using CommandLine;
using NLog;
using NLog.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NLog.Targets;


namespace Smi.Common.Options
{
    /// <summary>
    /// Base class for all Program entry points. Parses Cli options and sets-up a standard logging configuration
    /// </summary>
    public static class SmiCliInit
    {
        public static bool InitSmiLogging { get; set; } = true;

        public static readonly string HostProcessName;

        private static readonly Parser _parser;

        static SmiCliInit()
        {
            ParserSettings defaults = Parser.Default.Settings;
            _parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.CaseSensitive = false;
                settings.EnableDashDash = defaults.EnableDashDash;
                settings.HelpWriter = defaults.HelpWriter;
                settings.IgnoreUnknownArguments = false;
                settings.MaximumDisplayWidth = defaults.MaximumDisplayWidth;
                settings.ParsingCulture = defaults.ParsingCulture;
            });

            HostProcessName = Assembly.GetEntryAssembly()?.GetName().Name ?? throw new ApplicationException("Couldn't get the Assembly name!");
        }

        /// <summary>
        /// Parse CLI arguments to the specified type, and runs the provided function if parsing is successful
        /// </summary>
        /// <param name="args">Arguments passed to Main</param>
        /// <param name="onParse">The function to call on a successful parse</param>
        /// <returns>The return code from the onParse function</returns>
        public static int ParseAndRun<T>(IEnumerable<string> args, Func<GlobalOptions, T, int> onParse) where T : CliOptions
        {
            int ret = _parser
                .ParseArguments<T>(args)
                .MapResult(
                    parsed =>
                    {
                        GlobalOptions globals = new GlobalOptionsFactory().Load(parsed);

                        if (InitSmiLogging)
                            SmiLogging.Setup(globals.LoggingOptions, HostProcessName);

                        return onParse(globals, parsed);
                    },
                    OnErrors
                );
            return ret;
        }

        /// <summary>
        /// Parse CLI arguments to one of the specified types, and runs the provided function if parsing is successful
        /// </summary>
        /// <param name="args">Arguments passed to Main</param>
        /// <param name="targetVerbTypes">The list of possible target verb types to construct from the args</param>
        /// <param name="onParse">The function to call on a successful parse</param>
        /// <returns>The return code from the onParse function</returns>
        public static int ParseAndRun(IEnumerable<string> args, Type[] targetVerbTypes, Func<GlobalOptions, object, int> onParse)
        {
            int ret = _parser
                .ParseArguments(
                    args,
                    targetVerbTypes
                )
                .MapResult(
                    parsed =>
                    {
                        var cliOptions = Verify<CliOptions>(parsed);
                        GlobalOptions globals = new GlobalOptionsFactory().Load(cliOptions);

                        if (InitSmiLogging)
                            SmiLogging.Setup(globals.LoggingOptions, HostProcessName);

                        return onParse(globals, parsed);
                    },
                    OnErrors
                );
            return ret;
        }

        public static int ParseServiceVerbAndRun(IEnumerable<string> args, Type[] targetVerbTypes, Func<object, int> onParse)
        {
            int ret = _parser
                .ParseArguments(
                    args,
                    targetVerbTypes
                )
                .MapResult(
                    parsed => onParse(parsed),
                    OnErrors
                );
            return ret;
        }

        /// <summary>
        /// Verify the parsedOptions is of the specified type, or throw an exception
        /// </summary>
        /// <typeparam name="T">The type to check for</typeparam>
        /// <param name="parsedOptions"></param>
        /// <returns></returns>
        public static T Verify<T>(object parsedOptions)
        {
            if (!(parsedOptions is T asExpected))
                throw new NotImplementedException($"Did not construct expected type '{typeof(T).Name}'");
            return asExpected;
        }

        private static int OnErrors(IEnumerable<Error> errors)
        {
            // Create a default console logger - SMI one may not be available at this point
            var config = new LoggingConfiguration();
            using var consoleTarget = new ConsoleTarget(nameof(SmiCliInit));
            config.AddRule(LogLevel.Trace, LogLevel.Fatal, consoleTarget);
            Logger logger = LogManager.GetCurrentClassLogger();

            List<Error> errorsList = errors.ToList();
            if (errorsList.Count == 1 && errorsList.Single().Tag == ErrorType.HelpRequestedError)
                return 0;

            foreach (Error error in errorsList)
                logger.Error(error);

            return 1;
        }
    }
}
