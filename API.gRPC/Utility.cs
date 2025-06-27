using Neighborly;
using Neighborly.API.Protos;
using Neighborly.Search;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Neighborly.API
{
    public static class Utility
    {
        public static Neighborly.Vector ConvertToVector(VectorMessage vectorMessage)
        {
            // Convert the ByteString to a byte array
            byte[] values = vectorMessage.Values.ToByteArray();

            // Create a new Vector with the byte array
            Neighborly.Vector vector = new Neighborly.Vector(values);

            return vector;
        }

        public static VectorMessage ConvertToVectorMessage(Vector vector)
        {
            // Convert the byte array to a ByteString
            Google.Protobuf.ByteString values = Google.Protobuf.ByteString.CopyFrom(vector.ToBinary());

            // Create a new VectorMessage with the ByteString
            VectorMessage vectorMessage = new VectorMessage { Values = values };

            return vectorMessage;
        }

        /// <summary>
        /// Converts a proto MetadataFilter to a domain MetadataFilter
        /// </summary>
        public static Neighborly.MetadataFilter? ConvertToMetadataFilter(Protos.MetadataFilter? protoFilter)
        {
            if (protoFilter == null || protoFilter.Expressions.Count == 0)
                return null;

            var domainFilter = new Neighborly.MetadataFilter
            {
                Logic = ConvertToFilterLogic(protoFilter.Logic)
            };

            foreach (var expression in protoFilter.Expressions)
            {
                var filterValue = ConvertToFilterValue(expression.Value);
                if (filterValue != null)
                {
                    domainFilter.Filters[expression.Key] = filterValue;
                }
            }

            return domainFilter.HasFilters ? domainFilter : null;
        }

        /// <summary>
        /// Converts a domain MetadataFilter to a proto MetadataFilter
        /// </summary>
        public static Protos.MetadataFilter? ConvertToProtoMetadataFilter(Neighborly.MetadataFilter? domainFilter)
        {
            if (domainFilter == null || !domainFilter.HasFilters)
                return null;

            var protoFilter = new Protos.MetadataFilter
            {
                Logic = ConvertToProtoFilterLogic(domainFilter.Logic)
            };

            foreach (var kvp in domainFilter.Filters)
            {
                var protoExpression = new Protos.FilterExpression
                {
                    Key = kvp.Key,
                    Value = ConvertToProtoFilterValue(kvp.Value)
                };
                protoFilter.Expressions.Add(protoExpression);
            }

            return protoFilter;
        }

        /// <summary>
        /// Converts a proto SearchAlgorithm to a domain SearchAlgorithm
        /// </summary>
        public static Neighborly.Search.SearchAlgorithm ConvertToSearchAlgorithm(Protos.SearchAlgorithm protoAlgorithm)
        {
            return protoAlgorithm switch
            {
                Protos.SearchAlgorithm.Kdtree => Neighborly.Search.SearchAlgorithm.KDTree,
                Protos.SearchAlgorithm.BallTree => Neighborly.Search.SearchAlgorithm.BallTree,
                Protos.SearchAlgorithm.Linear => Neighborly.Search.SearchAlgorithm.Linear,
                Protos.SearchAlgorithm.Lsh => Neighborly.Search.SearchAlgorithm.LSH,
                Protos.SearchAlgorithm.Hnsw => Neighborly.Search.SearchAlgorithm.HNSW,
                Protos.SearchAlgorithm.BinaryQuantization => Neighborly.Search.SearchAlgorithm.BinaryQuantization,
                Protos.SearchAlgorithm.ProductQuantization => Neighborly.Search.SearchAlgorithm.ProductQuantization,
                _ => Neighborly.Search.SearchAlgorithm.KDTree
            };
        }

        /// <summary>
        /// Converts a domain SearchAlgorithm to a proto SearchAlgorithm
        /// </summary>
        public static Protos.SearchAlgorithm ConvertToProtoSearchAlgorithm(Neighborly.Search.SearchAlgorithm domainAlgorithm)
        {
            return domainAlgorithm switch
            {
                Neighborly.Search.SearchAlgorithm.KDTree => Protos.SearchAlgorithm.Kdtree,
                Neighborly.Search.SearchAlgorithm.BallTree => Protos.SearchAlgorithm.BallTree,
                Neighborly.Search.SearchAlgorithm.Linear => Protos.SearchAlgorithm.Linear,
                Neighborly.Search.SearchAlgorithm.LSH => Protos.SearchAlgorithm.Lsh,
                Neighborly.Search.SearchAlgorithm.HNSW => Protos.SearchAlgorithm.Hnsw,
                Neighborly.Search.SearchAlgorithm.BinaryQuantization => Protos.SearchAlgorithm.BinaryQuantization,
                Neighborly.Search.SearchAlgorithm.ProductQuantization => Protos.SearchAlgorithm.ProductQuantization,
                _ => Protos.SearchAlgorithm.Kdtree
            };
        }

        private static Neighborly.FilterLogic ConvertToFilterLogic(Protos.FilterLogic protoLogic)
        {
            return protoLogic switch
            {
                Protos.FilterLogic.And => Neighborly.FilterLogic.And,
                Protos.FilterLogic.Or => Neighborly.FilterLogic.Or,
                _ => Neighborly.FilterLogic.And
            };
        }

        private static Protos.FilterLogic ConvertToProtoFilterLogic(Neighborly.FilterLogic domainLogic)
        {
            return domainLogic switch
            {
                Neighborly.FilterLogic.And => Protos.FilterLogic.And,
                Neighborly.FilterLogic.Or => Protos.FilterLogic.Or,
                _ => Protos.FilterLogic.And
            };
        }

        private static Neighborly.FilterValue? ConvertToFilterValue(Protos.FilterValue protoValue)
        {
            if (protoValue == null)
                return null;

            var domainOperator = ConvertToFilterOperator(protoValue.Operator);
            
            object? value = protoValue.ValueTypeCase switch
            {
                Protos.FilterValue.ValueTypeOneofCase.StringValue => protoValue.StringValue,
                Protos.FilterValue.ValueTypeOneofCase.NumericValue => protoValue.NumericValue,
                Protos.FilterValue.ValueTypeOneofCase.BooleanValue => protoValue.BooleanValue,
                Protos.FilterValue.ValueTypeOneofCase.ArrayValue => protoValue.ArrayValue.Values.ToArray(),
                Protos.FilterValue.ValueTypeOneofCase.DatetimeValue => ParseDateTime(protoValue.DatetimeValue),
                _ => null
            };

            return value != null ? new Neighborly.FilterValue(value, domainOperator) : null;
        }

        private static Protos.FilterValue ConvertToProtoFilterValue(Neighborly.FilterValue domainValue)
        {
            var protoValue = new Protos.FilterValue
            {
                Operator = ConvertToProtoFilterOperator(domainValue.Operator)
            };

            switch (domainValue.Value)
            {
                case string stringVal:
                    protoValue.StringValue = stringVal;
                    break;
                case int intVal:
                    protoValue.NumericValue = intVal;
                    break;
                case long longVal:
                    protoValue.NumericValue = longVal;
                    break;
                case float floatVal:
                    protoValue.NumericValue = floatVal;
                    break;
                case double doubleVal:
                    protoValue.NumericValue = doubleVal;
                    break;
                case bool boolVal:
                    protoValue.BooleanValue = boolVal;
                    break;
                case DateTime dateTimeVal:
                    protoValue.DatetimeValue = dateTimeVal.ToString("O", CultureInfo.InvariantCulture);
                    break;
                case string[] stringArray:
                    protoValue.ArrayValue = new Protos.StringArray();
                    protoValue.ArrayValue.Values.AddRange(stringArray);
                    break;
                default:
                    protoValue.StringValue = domainValue.Value?.ToString() ?? string.Empty;
                    break;
            }

            return protoValue;
        }

        private static Neighborly.FilterOperator ConvertToFilterOperator(Protos.FilterOperator protoOperator)
        {
            return protoOperator switch
            {
                Protos.FilterOperator.Equals => Neighborly.FilterOperator.Equals,
                Protos.FilterOperator.NotEquals => Neighborly.FilterOperator.NotEquals,
                Protos.FilterOperator.GreaterThan => Neighborly.FilterOperator.GreaterThan,
                Protos.FilterOperator.LessThan => Neighborly.FilterOperator.LessThan,
                Protos.FilterOperator.GreaterEqual => Neighborly.FilterOperator.GreaterEqual,
                Protos.FilterOperator.LessEqual => Neighborly.FilterOperator.LessEqual,
                Protos.FilterOperator.Contains => Neighborly.FilterOperator.Contains,
                Protos.FilterOperator.NotContains => Neighborly.FilterOperator.NotContains,
                Protos.FilterOperator.In => Neighborly.FilterOperator.In,
                Protos.FilterOperator.NotIn => Neighborly.FilterOperator.NotIn,
                Protos.FilterOperator.Regex => Neighborly.FilterOperator.Regex,
                Protos.FilterOperator.StartsWith => Neighborly.FilterOperator.StartsWith,
                Protos.FilterOperator.EndsWith => Neighborly.FilterOperator.EndsWith,
                _ => Neighborly.FilterOperator.Equals
            };
        }

        private static Protos.FilterOperator ConvertToProtoFilterOperator(Neighborly.FilterOperator domainOperator)
        {
            return domainOperator switch
            {
                Neighborly.FilterOperator.Equals => Protos.FilterOperator.Equals,
                Neighborly.FilterOperator.NotEquals => Protos.FilterOperator.NotEquals,
                Neighborly.FilterOperator.GreaterThan => Protos.FilterOperator.GreaterThan,
                Neighborly.FilterOperator.LessThan => Protos.FilterOperator.LessThan,
                Neighborly.FilterOperator.GreaterEqual => Protos.FilterOperator.GreaterEqual,
                Neighborly.FilterOperator.LessEqual => Protos.FilterOperator.LessEqual,
                Neighborly.FilterOperator.Contains => Protos.FilterOperator.Contains,
                Neighborly.FilterOperator.NotContains => Protos.FilterOperator.NotContains,
                Neighborly.FilterOperator.In => Protos.FilterOperator.In,
                Neighborly.FilterOperator.NotIn => Protos.FilterOperator.NotIn,
                Neighborly.FilterOperator.Regex => Protos.FilterOperator.Regex,
                Neighborly.FilterOperator.StartsWith => Protos.FilterOperator.StartsWith,
                Neighborly.FilterOperator.EndsWith => Protos.FilterOperator.EndsWith,
                _ => Protos.FilterOperator.Equals
            };
        }

        private static DateTime? ParseDateTime(string? dateTimeString)
        {
            if (string.IsNullOrEmpty(dateTimeString))
                return null;

            if (DateTime.TryParse(dateTimeString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var result))
                return result;

            return null;
        }
    }
}
