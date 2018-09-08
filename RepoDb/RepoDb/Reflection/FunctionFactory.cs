﻿using RepoDb.Enumerations;
using RepoDb.Exceptions;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;

namespace RepoDb.Reflection
{
    /// <summary>
    /// A static factor class used to create a custom function.
    /// </summary>
    public static class FunctionFactory
    {
        /// <summary>
        /// Gets a designated expression function for converting the <see cref="DbDataReader"/> object into class entity.
        /// </summary>
        /// <typeparam name="TEntity">The target entity type.</typeparam>
        /// <param name="reader">The data reader object.</param>
        /// <returns>The entity instance with the converted values from the data reader.</returns>
        public static Func<DbDataReader, TEntity> GetDataReaderToDataEntityFunction<TEntity>(DbDataReader reader)
            where TEntity : class
        {
            // Expression variables
            var readerParameterExpression = Expression.Parameter(typeof(DbDataReader), "reader");
            var newEntityExpression = Expression.New(typeof(TEntity));

            // Matching the fields
            var fields = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .Select((n, i) => new DataReaderFieldDefinition
                {
                    Name = n.ToLower(),
                    Ordinal = i,
                    Type = reader.GetFieldType(i)
                });

            // Get the bindings
            var bindings = GetBindings<TEntity>(newEntityExpression, readerParameterExpression, fields);
            
            // Throw an error if there are no matching atleast one
            if (bindings.Any() == false)
            {
                throw new NoMatchedFieldsException($"There is no matching fields between the result set of the data reader and the type '{typeof(TEntity).FullName}'.");
            }

            // Initialize the members
            var body = Expression.MemberInit(newEntityExpression, bindings);

            // Set the function value
            return Expression
                .Lambda<Func<DbDataReader, TEntity>>(body, readerParameterExpression)
                .Compile();
        }

        /// <summary>
        /// Returns the list of the bindings for the entity.
        /// </summary>
        /// <param name="newEntityExpression">The new entity expression.</param>
        /// <param name="readerParameterExpression">The data reader parameter.</param>
        /// <param name="fields">The list of fields to be bound.</param>
        /// <returns>The enumerable list of member assignment and bindings.</returns>
        private static IEnumerable<MemberAssignment> GetBindings<TEntity>(Expression newEntityExpression,
            ParameterExpression readerParameterExpression, IEnumerable<DataReaderFieldDefinition> fields)
            where TEntity : class
        {
            // Initialize variables
            var bindings = new List<MemberAssignment>();
            var dataReaderType = typeof(DbDataReader);
            var fieldDefinitions = FieldDefinitionCache.Get<TEntity>(Command.Query);
            var properties = PropertyCache.Get<TEntity>(Command.Query)
                .Where(property => property.PropertyInfo.CanWrite)
                .ToList();

            // Iterate each properties
            properties?.ForEach(property =>
            {
                // Get the mapped name, definition, and ordinal
                var mappedName = property.GetMappedName().ToLower();
                var fieldDefinition = fieldDefinitions?.FirstOrDefault(fd => fd.Name.ToLower() == mappedName);
                var field = fields.FirstOrDefault(f => f.Name == mappedName);
                var ordinal = fields.Select(f => f.Name).ToList().IndexOf(mappedName);

                // Process only if there is a correct ordinal
                if (ordinal >= 0)
                {
                    // Identify the value for null check
                    var isNullable = fieldDefinition == null || fieldDefinition?.IsNullable == true;

                    // Get the type
                    var underlyingType = Nullable.GetUnderlyingType(property.PropertyInfo.PropertyType);
                    var propertyType = underlyingType ?? property.PropertyInfo.PropertyType;
                    var isConversionNeeded = field?.Type != propertyType;

                    // Get the correct method info, if the reader.Get<Type> is not found, then use the default GetValue
                    var readerGetValueMethod = dataReaderType.GetMethod($"Get{field?.Type.Name}");
                    if (readerGetValueMethod == null)
                    {
                        readerGetValueMethod = dataReaderType.GetMethod($"Get{propertyType.Name}") ??
                            dataReaderType.GetMethod("GetValue");
                        isConversionNeeded = true; // Force
                    }

                    // Expressions
                    var ordinalExpression = Expression.Constant(ordinal);
                    var valueExpression = (Expression)null;

                    // Check for nullables
                    if (isNullable == true)
                    {
                        var isDbNullExpression = Expression.Call(readerParameterExpression, dataReaderType.GetMethod("IsDBNull"), ordinalExpression);
                        var ifTrueExpression = Expression.Default(propertyType);
                        var isFalseExpression = (Expression)Expression.Call(readerParameterExpression, readerGetValueMethod, ordinalExpression);
                        if (isConversionNeeded == true)
                        {
                            isFalseExpression = Expression.Convert(isFalseExpression, propertyType);
                        }
                        valueExpression = Expression.Condition(isDbNullExpression, ifTrueExpression, isFalseExpression);
                    }
                    else
                    {
                        // Call the actual Get<Type>/GetValue method by ordinal
                        valueExpression = Expression.Call(readerParameterExpression,
                            readerGetValueMethod,
                            ordinalExpression);
                        if (isConversionNeeded == true)
                        {
                            valueExpression = Expression.Convert(valueExpression, propertyType);
                        }
                    }

                    // Check if value expression is not null, only add those
                    if (valueExpression != null)
                    {
                        // Check if the property is a 'Nullable' property
                        if (underlyingType != null && underlyingType.IsValueType == true)
                        {
                            // Create a new instance of nullable
                            var nullableConstructorExpression = typeof(Nullable<>).MakeGenericType(propertyType).GetConstructor(new[] { propertyType });
                            valueExpression = Expression.New(nullableConstructorExpression, valueExpression);
                        }

                        // Set the actual property value
                        bindings.Add(Expression.Bind(property.PropertyInfo, valueExpression));
                    }
                }
            });

            // Return the result
            return bindings;
        }
    }
}