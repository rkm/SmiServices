﻿using Newtonsoft.Json;

namespace Smi.Common.Messages.Extraction
{
    public class ExtractionRequestInfoMessage : ExtractMessage
    {
        [JsonProperty(Required = Required.Always)]
        public string KeyTag { get; set; }

        [JsonProperty(Required = Required.Always)]
        public int KeyValueCount { get; set; }

        [JsonProperty(Required = Required.Default)]
        public string ExtractionModality { get; set; }


        [JsonConstructor]
        public ExtractionRequestInfoMessage() { }

        public override string ToString()
        {
            return base.ToString() + $",KeyTag={KeyTag},KeyValueCount={KeyValueCount},ExtractionModality={ExtractionModality}";
        }
    }
}
