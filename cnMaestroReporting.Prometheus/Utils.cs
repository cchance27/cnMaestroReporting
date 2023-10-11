using System;
using System.Collections.Generic;

namespace cnMaestroReporting.Prometheus.Utils
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

       
        public static decimal FromTo(Unit unitFrom, decimal sizeFrom, Unit unitTo, int? decimalPlaces = null)
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
        public static double FromTo(Unit unitFrom, double sizeFrom, Unit unitTo, int? decimalPlaces = null)
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
        private static decimal ConvertTo(Unit unit, decimal bytes, int? decimalPlaces = null)
        {
            return Convert(Conversion.To, bytes, unit, decimalPlaces);
        }

        // Converts a measurement unit to bytes
        private static decimal ConvertFrom(Unit unit, decimal bytes, int? decimalPlaces = null)
        {
            return Convert(Conversion.From, bytes, unit, decimalPlaces);
        }
        // Converts bytes to a measurement unit
        private static double ConvertTo(Unit unit, double bytes, int? decimalPlaces = null)
        {
            return Convert(Conversion.To, bytes, unit, decimalPlaces);
        }

        // Converts a measurement unit to bytes
        private static double ConvertFrom(Unit unit, double bytes, int? decimalPlaces = null)
        {
            return Convert(Conversion.From, bytes, unit, decimalPlaces);
        }

        private static decimal Convert(Conversion operation, decimal bytes, Unit unit, int? decimalPlaces)
        {
            // Get the unit type definition
            var definition = definitions[unit];
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

        private static double Convert(Conversion operation, double bytes, Unit unit, int? decimalPlaces)
        {
            // Get the unit type definition
            var definition = definitions[unit];
            if (definition == null)
            {
                throw new ArgumentException($"Unknown unit type: {unit}", nameof(unit));
            }

            // Get the unit value
            var value = definition.ValueDouble;

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

            public double ValueDouble
            {
                get
                {
                    return Math.Pow((double)Multiple, OrderOfMagnitude);
                }
            }
        }

        private static Dictionary<Unit, Definition> definitions = new Dictionary<Unit, Definition>() {
                    { Unit.Kilobyte, new Definition() { OrderOfMagnitude = 1, Prefix = Prefix.Decimal } },
                    { Unit.Megabyte, new Definition() { OrderOfMagnitude = 2, Prefix = Prefix.Decimal } },
                    { Unit.Gigabyte, new Definition() { OrderOfMagnitude = 3, Prefix = Prefix.Decimal } },
                    { Unit.Terabyte, new Definition() { OrderOfMagnitude = 4, Prefix = Prefix.Decimal } },
                    { Unit.Petabyte, new Definition() { OrderOfMagnitude = 5, Prefix = Prefix.Decimal } },
                    { Unit.Exabyte, new Definition() { OrderOfMagnitude = 6, Prefix = Prefix.Decimal } },
                    { Unit.Zettabyte, new Definition() { OrderOfMagnitude = 7, Prefix = Prefix.Decimal } },
                    { Unit.Yottabyte, new Definition() { OrderOfMagnitude = 8, Prefix = Prefix.Decimal } },
                    { Unit.Kibibyte, new Definition() { OrderOfMagnitude = 1, Prefix = Prefix.Binary } },
                    { Unit.Mebibyte, new Definition() { OrderOfMagnitude = 2, Prefix = Prefix.Binary } },
                    { Unit.Gibibyte, new Definition() { OrderOfMagnitude = 3, Prefix = Prefix.Binary } },
                    { Unit.Tebibyte, new Definition() { OrderOfMagnitude = 4, Prefix = Prefix.Binary } },
                    { Unit.Pebibyte, new Definition() { OrderOfMagnitude = 5, Prefix = Prefix.Binary } },
                    { Unit.Exbibyte, new Definition() { OrderOfMagnitude = 6, Prefix = Prefix.Binary } },
                    { Unit.Zebibyte, new Definition() { OrderOfMagnitude = 7, Prefix = Prefix.Binary } },
                    { Unit.Yobibyte, new Definition() { OrderOfMagnitude = 8, Prefix = Prefix.Binary } },
                };

        private static void AddDefinitions(Dictionary<Unit, Definition> definitions, Prefix prefix, IEnumerable<Unit> units)
        {
            int index = 1;
            foreach (var unit in units)
            {
                if (!definitions.ContainsKey(unit))
                {
                    definitions.TryAdd(unit, new Definition()
                    {
                        Prefix = prefix,
                        OrderOfMagnitude = index
                    });
                }
                ++index;
            }
        }

        public static decimal BytesToTerabytes(this decimal bytes)
        {
            return Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, bytes, Utils.Bytes.Unit.Terabyte, 2);
        }

        public static decimal KiloBytesToGigabytes(this decimal bytes)
        {
            return Utils.Bytes.FromTo(Utils.Bytes.Unit.Kilobyte, bytes, Utils.Bytes.Unit.Gigabyte, 2);
        }

        public static double BytesToTerabytes(this double bytes)
        {
            return Utils.Bytes.FromTo(Utils.Bytes.Unit.Byte, bytes, Utils.Bytes.Unit.Terabyte, 2);
        }

        public static double KiloBytesToGigabytes(this double bytes)
        {
            return Utils.Bytes.FromTo(Utils.Bytes.Unit.Kilobyte, bytes, Utils.Bytes.Unit.Gigabyte, 2);
        }
    }
} // http://programmingnotes.org/