﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using Smi.Common.Messages.Extraction;


namespace Applications.ExtractImages
{
    public class CohortCsvParser
    {
        // NOTE(rkm 2021-04-01) Just do a simple line-by-line read through the CSV
        private readonly CsvConfiguration _csvConfiguration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        };

        private readonly IFileSystem _fileSystem;

        public CohortCsvParser(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public Tuple<ExtractionKey, List<string>> Parse(string csvFilePath)
        {
            using Stream fileStream = _fileSystem.FileStream.Create(csvFilePath, FileMode.Open);
            using var streamReader = new StreamReader(fileStream);
            using var reader = new CsvReader(streamReader, _csvConfiguration);

            reader.Read();
            string[] headerRecord = reader.Parser.Record;

            if (headerRecord == null)
                throw new ApplicationException(message: "CSV is empty");

            if (headerRecord.Length != 1)
                throw new ApplicationException(message: "CSV must have exactly 1 column");

            if (!Enum.TryParse<ExtractionKey>(headerRecord[0], out ExtractionKey extractionKey))
            {
                string keys = string.Join(separator: ',', Enum.GetNames(typeof(ExtractionKey)));
                throw new ApplicationException($"CSV header must be a valid ExtractionKey: {keys}");
            }

            var allIds = new List<string>();
            while (reader.Read())
            {
                string[] record = reader.Parser.Record;
                if (record.Length != 1)
                    throw new ApplicationException(message: "CSV must have exactly 1 column");

                string id = record[0]?.Trim();
                if (!string.IsNullOrWhiteSpace(id))
                    allIds.Add(id);
            }

            if (!allIds.Any())
                throw new ApplicationException(message: "No records in the cohort CSV");

            return new Tuple<ExtractionKey, List<string>>(extractionKey, allIds);
        }
    }
}