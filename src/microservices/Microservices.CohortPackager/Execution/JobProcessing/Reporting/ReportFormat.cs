﻿namespace Microservices.CohortPackager.Execution.JobProcessing.Reporting
{
    /// <summary>
    /// Used to select the report format that should be generated
    /// </summary>
    public enum ReportFormat
    {
        Unknown,

        /// <summary>
        /// The "original" format, useful for smaller extractions. Produces a single report containing all the extraction information in a text-based format
        /// </summary>
        Combined,

        /// <summary>
        /// A pack of multiple report files containing:
        /// -   Text summary report with the extraction job metadata
        /// -   Validation report for pixel data - summary csv
        /// -   Validation report for pixel data - full csv with file listing
        /// -   Validation report for all other tags - summary csv
        /// -   Validation report for all other tags- full csv with file listing
        /// </summary>
        Split,
    }
}
