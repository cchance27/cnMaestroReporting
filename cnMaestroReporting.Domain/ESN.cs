using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace cnMaestroReporting.Domain
{
    public record ESN
    {
        private string _esn = "";

        public string value { get => _esn; set => _esn = validateAndStandardize(value); }

        public ESN(string esn)
        {
            this.value = validateAndStandardize(esn);
        }

        private string validateAndStandardize(string input)
        {
            var match = Regex.Match(input, @"([\d\w]{2}).?([\d\w]{2}).?([\d\w]{2}).?([\d\w]{2}).?([\d\w]{2}).?([\d\w]{2})");

            if (match.Groups.Count != 7)
                throw new InvalidDataException("Mac Address format is not recognized");

            return $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}-{match.Groups[4].Value}-{match.Groups[5].Value}-{match.Groups[6].Value}";
        }
        public static implicit operator ESN(string v)
        {
            return new ESN(v);
        }

        public static implicit operator string(ESN v)
        {
            return v.value;
        }
    }
}