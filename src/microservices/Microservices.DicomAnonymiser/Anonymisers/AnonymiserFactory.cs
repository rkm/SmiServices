using Smi.Common.Options;
using System;

namespace Microservices.DicomAnonymiser.Anonymisers
{
    public static class AnonymiserFactory
    {
        public static IDicomAnonymiser CreateAnonymiser(DicomAnonymiserOptions dicomAnonymiserOptions)
        {
            var anonymiserTypeStr = dicomAnonymiserOptions.AnonymiserType;
            if (!Enum.TryParse(anonymiserTypeStr, ignoreCase: true, out AnonymiserType anonymiserType))
                throw new ArgumentException($"Could not parse '{anonymiserTypeStr}' to a valid AnonymiserType");

            return anonymiserType switch
            {
                AnonymiserType.CTP => new CtpAnonymiser(dicomAnonymiserOptions.CtpAnonymiserOptions),
                _ => throw new NotImplementedException($"No case for AnonymiserType '{anonymiserType}'"),
            };
        }
    }
}
