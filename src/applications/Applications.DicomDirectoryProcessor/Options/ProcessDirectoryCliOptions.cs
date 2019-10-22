﻿
using System.Collections.Generic;
using System.IO;
using CommandLine;
using CommandLine.Text;
using Smi.Common.Options;
using ReusableLibraryCode.Annotations;

namespace Applications.DicomDirectoryProcessor.Options
{
    public class ProcessDirectoryCliOptions : CliOptions
    {
        [UsedImplicitly]
        [Option('d', "to-process", Required = true, HelpText = "The directory to process")]
        public string ToProcess { get; set; }

        [UsedImplicitly]
        [Option('f', "directory-format", Required = false, HelpText = "The specific directory search format to use", Default = "Default")]
        public string DirectoryFormat { get; set; }


        public DirectoryInfo ToProcessDir
        {
            get
            {
                return ToProcess == null
                    ? null
                    : new DirectoryInfo(ToProcess);
            }

            set
            {
                ToProcess = value != null
                    ? value.FullName
                    : null;
            }
        }

        [Usage]
        [UsedImplicitly]
        public static IEnumerable<Example> Examples
        {
            get
            {
                yield return
                    new Example("Normal Scenario", new ProcessDirectoryCliOptions { ToProcess = @"c:\temp\bob" });
                yield return
                    new Example("Override Yaml File", new ProcessDirectoryCliOptions { ToProcess = @"c:\temp\bob", YamlFile = "myconfig.yaml" });
                yield return
                    new Example("Search using the PACS directory structure", new ProcessDirectoryCliOptions { ToProcess = @"c:\temp\bob", DirectoryFormat = "PACS" });
            }
        }

        public override string ToString()
        {
            return base.ToString() + "ToProcess: \"" + ToProcess + ", DirectoryFormat" + DirectoryFormat + "\"\n";
        }
    }
}
