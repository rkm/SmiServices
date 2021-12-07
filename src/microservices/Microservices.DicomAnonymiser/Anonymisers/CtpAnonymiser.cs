using NLog;
using Smi.Common.Messages.Extraction;
using Smi.Common.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;

namespace Microservices.DicomAnonymiser.Anonymisers
{
    /// <summary>
    /// Wrapper for the CtpAnonymiser Java app
    /// 
    /// NOTE(rkm 2021-07-08) No point using the IFileSystem abstractions here, since there's
    /// no way of translating that to Java land.
    /// </summary>
    public class CtpAnonymiser : IDicomAnonymiser, IDisposable
    {
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();

        private readonly Process _ctpProc;

        private List<string> _errorData = new List<string>();

        public CtpAnonymiser(CtpAnonymiserOptions options)
        {
            if (!File.Exists(options.AnonScriptPath))
                throw new Exception($"Could not find anon script file ('{options.AnonScriptPath}')");

            var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
            if (string.IsNullOrWhiteSpace(javaHome))
                throw new Exception("JAVA_HOME not set");

            string javaPath = Path.Combine(javaHome, "bin", "java");

            if (string.IsNullOrWhiteSpace(options.CtpJarPath))
                throw new Exception("CtpJarPath not set");

            if (!File.Exists(options.CtpJarPath))
                throw new Exception($"Could not find '{options.CtpJarPath}'");

            _ctpProc = new Process();
            _ctpProc.StartInfo.FileName = javaPath;
            _ctpProc.StartInfo.Arguments = $"-jar {options.CtpJarPath} {options.AnonScriptPath}";
            _ctpProc.StartInfo.UseShellExecute = false;
            _ctpProc.StartInfo.ErrorDialog = false;
            _ctpProc.StartInfo.RedirectStandardInput = true;
            _ctpProc.StartInfo.RedirectStandardOutput = true;
            _ctpProc.StartInfo.RedirectStandardError = true;
            _ctpProc.ErrorDataReceived += (_, e) => _errorData.Add(e.Data);

            try
            {
                _logger.Debug("Starting CTP process");
                _ctpProc.Start();
                _ctpProc.BeginErrorReadLine();

                var resp = SendCommandSync("PING");

                if (resp != "PONG")
                    throw new Exception($"Unexpected response from ctp (exited={_ctpProc.HasExited}): {resp}");
            }
            catch (Exception e)
            {
                _logger.Error(e, "Could not init CTP process");
                Dispose();
                throw;
            }
        }

        private string SendCommandSync(string request)
        {
            _logger.Trace($"-> {request}");
            _ctpProc.StandardInput.WriteLine(request);
            _ctpProc.StandardInput.Flush();
            var response = _ctpProc.StandardOutput.ReadLine();
            _logger.Trace($"<- {response}");
            return response;
        }

        private void CheckErrorData()
        {
            if (_errorData.Count == 0)
                return;

            var e = new Exception("CTP process produced stderr");
            e.Data.Add("errorData", _errorData);
            throw e;
        }

        public ExtractedFileStatus Anonymise(IFileInfo sourceFile, IFileInfo destFile)
        {
            var resp = SendCommandSync($"ANON|{sourceFile}|{destFile}");

            if (!resp.Equals($"ANON_OK {destFile}"))
            {
                _logger.Error($"CTP did not return ANON_OK. Received '{resp}'");
                return ExtractedFileStatus.ErrorWontRetry;
            }

            return ExtractedFileStatus.Anonymised;
        }

        public void Dispose()
        {
            _logger.Debug("CtpAnonymiser.Dispose");

            if (_ctpProc == null)
                return;

            try
            {
                var resp = SendCommandSync("EXIT");
                if (resp != "BYE")
                    _logger.Error("Did not recieve BYE from CTP process");

                CheckErrorData();

                if (!_ctpProc.WaitForExit(10_000))
                    throw new Exception("CTP process did not exit in time");
            }
            catch (Exception e)
            {
                _logger.Error(e, "CTP process did not exit properly. Killing...");
                _ctpProc.Kill(entireProcessTree: true);
            }
            finally
            {
                _ctpProc.Close();
                _ctpProc.Dispose();
            }
        }
    }
}
