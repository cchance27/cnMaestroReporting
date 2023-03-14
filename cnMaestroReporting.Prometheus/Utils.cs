using System;
using System.Collections.Generic;

namespace Utils
{

    public static class StringExtensions
    {
        public static int DecimalStringToInt(this String str)
        {
            return Convert.ToInt32(Math.Round(decimal.Parse(str)));
        }
    }
    public static class DateTimeExtensions
    {
        public static string ToRFC3339(this DateTime date)
        {
            return date.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffK");
        }
    }// https://stackoverflow.com/questions/5017782/c-sharp-datetime-rfc-3339-format

    public static class Bytes
    {
        public enum Unit
        {
            Byte,

            // Decimal 
            Kilobyte,
            Megabyte,
            Gigabyte,
            Terabyte,
            Petabyte,
            Exabyte,
            Zettabyte,
            Yottabyte,

            // Binary 
            Kibibyte,
            Mebibyte,
            Gibibyte,
            Tebibyte,
            Pebibyte,
            Exbibyte,
            Zebibyte,
            Yobibyte
        }

        /// <summary>
        /// Converts a measurement 'Unit' to another measurement 'Unit'
        /// </summary>
        /// <param name="unitFrom">The measurement unit converting from</param> 
        /// <param name="sizeFrom">The size of the 'from' measurement unit</param> 
        /// <param name="unitTo">The measurement unit to convert to</param> 
        /// <param name="decimalPlaces">The decimal places to round to</param> 
        /// <returns>The value converted to the specified measurement unit</returns>
        public static decimal FromTo(Unit unitFrom, decimal sizeFrom, Unit unitTo
                    , int? decimalPlaces = null)
        {
            var result = sizeFrom;
            if (unitFrom != unitTo)
            {
                if (unitFrom == Unit.Byte)
                {
                    result = ConvertTo(unitTo, sizeFrom, decimalPlaces);
                }
                else if (unitTo == Unit.Byte)
                {
                    result = ConvertFrom(unitFrom, sizeFrom, decimalPlaces);
                }
                else
                {
                    result = ConvertTo(unitTo, ConvertFrom(unitFrom, sizeFrom), decimalPlaces);
                }
            }
            return result;
        }

        private enum Conversion
        {
            From,
            To
        }

        // Converts bytes to a measurement unit
        private static decimal ConvertTo(Unit unit, decimal bytes
                    , int? decimalPlaces = null)
        {
            return Convert(Conversion.To, bytes, unit, decimalPlaces);
        }

        // Converts a measurement unit to bytes
        private static decimal ConvertFrom(Unit unit, decimal bytes
                    , int? decimalPlaces = null)
        {
            return Convert(Conversion.From, bytes, unit, decimalPlaces);
        }

        private static decimal Convert(Conversion operation, decimal bytes, Unit unit
                    , int? decimalPlaces)
        {
            // Get the unit type definition
            var definition = GetDefinition(unit);
            if (definition == null)
            {
                throw new ArgumentException($"Unknown unit type: {unit}", nameof(unit));
            }

            // Get the unit value
            var value = definition.Value;

            // Calculate the result
            var result = operation == Conversion.To ? bytes / value : bytes * value;
            if (decimalPlaces.HasValue)
            {
                result = Math.Round(result, decimalPlaces.Value, MidpointRounding.AwayFromZero);
            }
            return result;
        }

        public enum Prefix
        {
            Decimal,
            Binary
        }

        public class Definition
        {
            public Prefix Prefix { get; set; }
            public int OrderOfMagnitude { get; set; }
            public decimal Multiple
            {
                get
                {
                    return Prefix == Prefix.Decimal ? 1000 : 1024;
                }
            }
            public decimal Value
            {
                get
                {
                    return System.Convert.ToDecimal(Math.Pow((double)Multiple, OrderOfMagnitude));
                }
            }
        }

        public static Definition GetDefinition(Unit unit)
        {
            var definitions = GetDefinitions();
            return definitions.ContainsKey(unit) ? definitions[unit] : null;
        }

        public static Dictionary<Unit, Definition> GetDefinitions()
        {
            if (definitions == null)
            {
                definitions = new Dictionary<Unit, Definition>();
                // Place units in order of magnitude

                // Decimal units
                var decimals = new[] {
                    Unit.Kilobyte,
                    Unit.Megabyte,
                    Unit.Gigabyte,
                    Unit.Terabyte,
                    Unit.Petabyte,
                    Unit.Exabyte,
                    Unit.Zettabyte,
                    Unit.Yottabyte
                };
                // Binary units
                var binary = new[] {
                    Unit.Kibibyte,
                    Unit.Mebibyte,
                    Unit.Gibibyte,
                    Unit.Tebibyte,
                    Unit.Pebibyte,
                    Unit.Exbibyte,
                    Unit.Zebibyte,
                    Unit.Yobibyte
                };
                AddDefinitions(definitions, Prefix.Decimal, decimals);
                AddDefinitions(definitions, Prefix.Binary, binary);
            }
            return definitions;
        }
        private static Dictionary<Unit, Definition> definitions = null;

        private static void AddDefinitions(Dictionary<Unit, Definition> definitions, Prefix prefix, IEnumerable<Unit> units)
        {
            int index = 1;
            foreach (var unit in units)
            {
                if (!definitions.ContainsKey(unit))
                {
                    definitions.Add(unit, new Definition()
                    {
                        Prefix = prefix,
                        OrderOfMagnitude = index
                    });
                }
                ++index;
            }
        }
    }
} // http://programmingnotes.org/