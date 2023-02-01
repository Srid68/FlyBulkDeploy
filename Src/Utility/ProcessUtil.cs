/*
This code is based on work from Mark Byers:
http://stackoverflow.com/a/7608823/83418
*/

using System.Diagnostics;
using Timer = System.Timers.Timer;

namespace Arshu.FlyDeploy.Utility
{
    public class CommandEventArgs : EventArgs
    {
        public string Message { get; set; } = "";
        public List<string> StdOutput { get; set; } = new List<string>();
        public List<string> ErrOutput { get; set; } = new List<string>();
    }

    public class ProcessExecute
    {
        private const int LONG_TIMEOUT_IN_MILLISEC = 1 * 60 * 60 * 1000  /* 1 hours! */;
        private const int HEARTBEAT_INTERVAL_IN_MILLISEC = 1 * 60 * 1000  /* 1 Min! */;
        private const int IDLE_TIMEOUT_IN_MILLISEC = 3 * 60 * 1000  /* 3 Min! */;
        private int processTimeout = LONG_TIMEOUT_IN_MILLISEC;
        private int heartbeatInterval = HEARTBEAT_INTERVAL_IN_MILLISEC;
        private int idleTimeout = IDLE_TIMEOUT_IN_MILLISEC;
        private string fileName;
        private string args;
        private int pid;
        private CancellationToken cancellationToken;
        private StreamWriter? inputWriter;

        public bool IsRunning { get; private set; }
        public List<string> Output { get; private set; } = new List<string>();

        public event EventHandler<CommandEventArgs> StatusReport = delegate { };

        private ProcessExecute() { throw new ArgumentOutOfRangeException("Please use paramaterized constructors"); }

        public ProcessExecute(string fileName, string args, Dictionary<string, string>? envVariableList, int heartbeatInterval = HEARTBEAT_INTERVAL_IN_MILLISEC, int idleTimeout = IDLE_TIMEOUT_IN_MILLISEC, int processTimeout = LONG_TIMEOUT_IN_MILLISEC, CancellationToken? cancellationToken = null)
        {
            this.IsRunning = false;
            this.fileName = fileName;
            this.args = args;
            this.processTimeout = processTimeout;
            this.heartbeatInterval = heartbeatInterval;
            this.idleTimeout = idleTimeout;
            this.cancellationToken = cancellationToken ?? CancellationToken.None;
            if (envVariableList != null)
            {
                _envVariableList = envVariableList;
            }
        }

        private Dictionary<string, string> _envVariableList = new Dictionary<string, string>();
        public bool AddEnvironmentVariables(string envKey, string envValue)
        {
            bool ret = false;
            if (_envVariableList.ContainsKey(envKey) == false)
            {
                _envVariableList.Add(envKey, envValue);
                ret = true;
            }
            return ret;
        }
        public bool RemoveEnvironmentVariables(string envKey)
        {
            bool ret = false;
            if (_envVariableList.ContainsKey(envKey) == false)
            {
                _envVariableList.Remove(envKey);
                ret = true;
            }
            return ret;
        }

        public List<string> Run(string workingDirectory = "", bool skipClear =false)
        {
            if (skipClear == false)
            {
                Output.Clear();
            }
            List<string> output = new List<string>();
            Process process = new Process();
            try
            {
                output = Strategy2(this.fileName, this.args, process, workingDirectory, this.cancellationToken);
            }
            finally
            {
                process.Dispose();
                this.IsRunning = false;
                this.pid = Int32.MaxValue;
            }
            if (skipClear == true)
            {
                output = Output;
            }
            return output;
        }

        public bool WriteInput(string input)
        {
            bool ret = false;

            if (inputWriter != null)
            {
                inputWriter.WriteLine(input);
                inputWriter.Flush();
            }
            return ret;
        }

        //TODO: is there an issue if the command doesn't output anything (or is this a pipe problem?)
        private List<string> Strategy2(string fileName, string args, Process process, string workingDirectory, CancellationToken cancellationToken)
        {
            var stdout = new List<string>();
            var stderr = new List<string>();

            long processLastActiveMs = 0;
            bool success = false;
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();

            //why do it this way? see 
            //http://stackoverflow.com/questions/139593/processstartinfo-hanging-on-waitforexit-why
            //NOTE: This code hangs when stepped through in SharpDevelop

            Timer heartBeat = new Timer(heartbeatInterval/*1200002 minutes*/);
            heartBeat.Elapsed += delegate
            {
                // prefer stderr messages (some exes only return status messages on stderr)
                string? status = stderr.Count > 0 ? stderr.Last() : stdout.DefaultIfEmpty("(none)").LastOrDefault();

                string message = "I own PID " + this.pid + " and I'm not dead yet! " +
                            "My process last reported " + status +
                             "(" + fileName + " " + args + ")";

                StatusReport(this, new CommandEventArgs { Message = message });

                if (stopWatch.ElapsedMilliseconds - processLastActiveMs > idleTimeout /*3000005 minutes*/)
                {
                    //log.Error("Background heartbeat observed that process pid=" + this.pid + " hasn't " +
                    //    " returned anything in 5 minutes. Shutting down zombie process");
                    heartBeat.Stop();

                    new ProcessExecute("taskkill", " /F /PID " + this.pid, null).Run();

                    process.Dispose();
                    this.IsRunning = false;
                }

            };
            heartBeat.Start();

            process.EnableRaisingEvents = true;
            if (string.IsNullOrEmpty(workingDirectory) == false)
            {
                process.StartInfo.WorkingDirectory = workingDirectory;
            }
            foreach (var item in _envVariableList)
            {
                process.StartInfo.EnvironmentVariables[item.Key] = item.Value;
            }
            process.StartInfo.FileName = fileName;
            process.StartInfo.Arguments = args;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            //for plink.exe, see http://social.msdn.microsoft.com/Forums/vstudio/en-US/697324fa-6ce6-4324-971a-61f6eec075be/redirecting-output-from-plink
            process.StartInfo.RedirectStandardInput = true; //redirecting standard input b/c plink.exe requires it
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;

            using (AutoResetEvent stdOutResetEvent = new AutoResetEvent(false))
            using (AutoResetEvent stdErrResetEvent = new AutoResetEvent(false))
            {
                process.OutputDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null || cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            stdOutResetEvent.Set();
                        }
                        catch (ObjectDisposedException) { }
                    }
                    else if (!String.IsNullOrEmpty(e.Data))
                    {
                        //Task.Run(() =>
                        //{
                            processLastActiveMs = stopWatch.ElapsedMilliseconds;
                            stdout.Add(e.Data);
                            Output.Add(e.Data);

                            StatusReport(this, new CommandEventArgs { Message = e.Data, StdOutput = stdout });
                        //});
                    }

                };
                process.ErrorDataReceived += delegate (object sender, DataReceivedEventArgs e)
                {
                    if (e.Data == null || cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            stdErrResetEvent.Set();
                        }
                        catch (ObjectDisposedException) { }
                    }
                    else if (!String.IsNullOrEmpty(e.Data))
                    {
                        processLastActiveMs = stopWatch.ElapsedMilliseconds;
                        stderr.Add(e.Data);

                        StatusReport(this, new CommandEventArgs { Message = e.Data, ErrOutput = stderr });
                    }
                };

                //-- Here we go ...
                process.Start();

                this.IsRunning = true;
                this.pid = process.Id;

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                inputWriter = process.StandardInput;

                success = process.WaitForExit(processTimeout) &&
                               stdErrResetEvent.WaitOne(processTimeout) &&
                               stdOutResetEvent.WaitOne(processTimeout);
            }

            heartBeat.Stop();
            
            if (!success)
            {
                string message = "Timed or caught out while executing command " +
                                fileName + " " + args + ". Agressively killing the process by PID=" + process.Id;

                new ProcessExecute("taskkill", "/F /PID " + process.Id, null).Run();

                throw new TimeoutException(message);
            }
            if (cancellationToken.IsCancellationRequested)
            {
                string message = "ABORTING process at cancellation request. Process was:" +
                                fileName + " " + args + ". Agressively killing the process by PID=" + process.Id;

                new ProcessExecute("taskkill", "/F /PID " + process.Id, null).Run();

                throw new ThreadInterruptedException(message);
            }
            //Task.Delay(5000);
            stdout.AddRange(stderr);
            return stdout;
        }

    }
}
