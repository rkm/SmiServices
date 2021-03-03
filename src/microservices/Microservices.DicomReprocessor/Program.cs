﻿using Microservices.DicomReprocessor.Execution;
using Microservices.DicomReprocessor.Options;
using Smi.Common.Execution;
using Smi.Common.Options;
using System.Collections.Generic;

namespace Microservices.DicomReprocessor
{
    public static class Program
    {
        public static int Main(IEnumerable<string> args)
        {
            int ret = SmiCliInit.ParseAndRun<DicomReprocessorCliOptions>(args, OnParse);
            return ret;
        }

        private static int OnParse(GlobalOptions globals, DicomReprocessorCliOptions opts)
        {
            var bootstrapper = new MicroserviceHostBootstrapper(() => new DicomReprocessorHost(globals, opts));
            int ret = bootstrapper.Main();
            return ret;
        }
    }
}
