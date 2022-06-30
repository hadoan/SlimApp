using System;
using System.Linq.Expressions;
using SlimApp.Timing;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace SlimApp.EntityFrameworkCore.ValueConverters
{
    public class AbpDateTimeValueConverter : ValueConverter<DateTime?, DateTime?>
    {
        public AbpDateTimeValueConverter(ConverterMappingHints mappingHints = null)
            : base(Normalize, Normalize, mappingHints)
        {
        }

        private static readonly Expression<Func<DateTime?, DateTime?>> Normalize = x =>
            x.HasValue ? Clock.Normalize(x.Value) : x;
    }
}