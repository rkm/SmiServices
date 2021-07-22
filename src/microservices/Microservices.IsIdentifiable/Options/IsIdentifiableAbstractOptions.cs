﻿using CommandLine;
using FAnsi;
using Smi.Common.Options;


namespace Microservices.IsIdentifiable.Options
{
    public abstract class IsIdentifiableAbstractOptions : CliOptions
    {
        [Option(HelpText = "Optional. Full connection string to the database storing the whitelist of valid entries")]
        public string WhitelistConnectionString { get; set; }

        [Option(HelpText = "Optional. The DBMS provider of the whitelist table e.g. MySql")]
        public DatabaseType WhitelistDatabaseType { get; set; }

        [Option(HelpText = "Optional. The unqualified name of the whitelist table")]
        public string WhitelistTableName { get; set; }

        [Option(HelpText = "Optional. The column in WhitelistTableName which contains the whitelist elements")]
        public string WhitelistColumn { get; set; }

        [Option(HelpText = "Optional. Path to a CSV file containing a single untitled column of whitelist values")]
        public string WhitelistCsv { get; set; }

        [Option(HelpText = "Optional. Generate a report on the proportion of values failing validation (for each column)")]
        public bool ColumnReport { get; set; }

        [Option(HelpText = "Optional. Generate a report listing every unique value failing validation (and the column the value failed in)")]
        public bool ValuesReport { get; set; }

        [Option(HelpText = "Optional. Generate a full failure storage report that persists Failure objects in a manner that they can be retrieved.")]
        public bool StoreReport { get; set; }

        [Option(HelpText = "Optional - If specified reports will be generated in the given folder.  If not specified, current directory is used (unless an alternate destination option is picked)")]
        public string DestinationCsvFolder { get; set; }

        [Option(HelpText = @"Optional - If specified, the given separator will be used instead of ,.  Includes support for \t for tab and \r\n.")]
        public string DestinationCsvSeparator { get; set; }

        [Option(HelpText = @"Optional - If specified all tabs, newlines (\r and \n) and 2+ spaces will be stripped from the values written as output (applies to all output formats)")]
        public bool DestinationNoWhitespace { get; set; }

        [Option(HelpText = "Optional. Full connection string to the database in which to store the report results")]
        public string DestinationConnectionString { get; set; }

        [Option(HelpText = "Optional. The DBMS provider of DestinationConnectionString e.g. MySql")]
        public DatabaseType DestinationDatabaseType { get; set; }

        [Option(HelpText = "Optional. If specified postcodes will not be reported as failures")]
        public bool IgnorePostcodes { get; set; }

        [Option(HelpText = "Optional. Comma separated list of columns/tags which should be ignored and not processed")]
        public string SkipColumns { get; set; }

        [Option(HelpText = "Optional. If set and using a 7 class NER model then DATE and TIME objects will not be considered failures.")]
        public bool IgnoreDatesInText { get; set; }

        [Option(HelpText = "Optional. Set to control the max size of the in-memory store of processed before the get written out to any destinations. Only makes sense for reports that don't perform any aggregation across the data", Default = MaxCacheSizeDefault)]
        public int MaxCacheSize { get; set; } = MaxCacheSizeDefault;

        public const int MaxCacheSizeDefault = 10000;

        [Option(HelpText = "Optional. Filename of additional rules in yaml format.")]
        public string RulesFile { get; set; }

        [Option(HelpText = "Optional. Directory of additional rules in yaml format.")]
        public string RulesDirectory { get; set; }

        [Option(HelpText = "Optional.  Maximum number of answers to cache per column.", Default = MaxValidationCacheSizeDefault)]
        public int MaxValidationCacheSize { get; set; } = MaxValidationCacheSizeDefault;

        public const int MaxValidationCacheSizeDefault = 1_000_000;

        /// <summary>
        /// Returns a short string with no spaces or punctuation that describes the target.  This will be used
        /// for naming output reports e.g. "biochemistry" , "mydir" etc
        /// </summary>
        /// <returns></returns>
        public abstract string GetTargetName();

        /// <summary>
        /// Throw exceptions if the selected options are incompatible
        /// </summary>
        public virtual void ValidateOptions()
        {

        }


        /// <summary>
        /// Populates class options that have not been specified on the command line directly by using the values (if any) in the
        /// default yaml file for smi services
        /// </summary>
        /// <param name="globalOpts"></param>
        public virtual void FillMissingWithValuesUsing(IsIdentifiableOptions globalOpts)
        {
            if (string.IsNullOrWhiteSpace(WhitelistConnectionString))
                WhitelistConnectionString = globalOpts.WhitelistConnectionString;

            if (WhitelistDatabaseType == default(DatabaseType) && globalOpts.WhitelistDatabaseType.HasValue)
                WhitelistDatabaseType = globalOpts.WhitelistDatabaseType.Value;
            
            if (string.IsNullOrWhiteSpace(WhitelistTableName))
                WhitelistTableName = globalOpts.WhitelistTableName;

            if (string.IsNullOrWhiteSpace(WhitelistColumn))
                WhitelistColumn = globalOpts.WhitelistColumn;

            if (string.IsNullOrWhiteSpace(WhitelistCsv))
                WhitelistCsv = globalOpts.WhitelistCsv;

            if (ColumnReport == default(bool) && globalOpts.ColumnReport.HasValue)
                ColumnReport = globalOpts.ColumnReport.Value;

            if (ValuesReport == default(bool) && globalOpts.ValuesReport.HasValue)
                ValuesReport = globalOpts.ValuesReport.Value;

            if (StoreReport == default(bool) && globalOpts.StoreReport.HasValue)
                StoreReport = globalOpts.StoreReport.Value;

            if (string.IsNullOrWhiteSpace(DestinationCsvFolder))
                DestinationCsvFolder = globalOpts.DestinationCsvFolder;

            if (string.IsNullOrWhiteSpace(DestinationCsvSeparator))
                DestinationCsvSeparator = globalOpts.DestinationCsvSeparator;

            if (DestinationNoWhitespace == default(bool) && globalOpts.DestinationNoWhitespace.HasValue)
                DestinationNoWhitespace = globalOpts.DestinationNoWhitespace.Value;

            if (string.IsNullOrWhiteSpace(DestinationConnectionString))
                DestinationConnectionString = globalOpts.DestinationConnectionString;

            if (DestinationDatabaseType == default(DatabaseType) && globalOpts.DestinationDatabaseType.HasValue)
                DestinationDatabaseType = globalOpts.DestinationDatabaseType.Value;

            if (IgnorePostcodes == default(bool) && globalOpts.IgnorePostcodes.HasValue)
                IgnorePostcodes = globalOpts.IgnorePostcodes.Value;

            if (string.IsNullOrWhiteSpace(SkipColumns))
                SkipColumns = globalOpts.SkipColumns;

            if (IgnoreDatesInText == default(bool) && globalOpts.IgnoreDatesInText.HasValue)
                IgnoreDatesInText = globalOpts.IgnoreDatesInText.Value;

            if (MaxCacheSize == MaxCacheSizeDefault && globalOpts.MaxCacheSize.HasValue)
                MaxCacheSize = globalOpts.MaxCacheSize.Value;

            if (string.IsNullOrWhiteSpace(RulesFile))
                RulesFile = globalOpts.RulesFile;

            if (string.IsNullOrWhiteSpace(RulesDirectory))
                RulesDirectory = globalOpts.RulesDirectory;

            if (MaxValidationCacheSize == MaxValidationCacheSizeDefault && globalOpts.MaxValidationCacheSize.HasValue)
                MaxValidationCacheSize = globalOpts.MaxValidationCacheSize.Value;
        }
    }
}
