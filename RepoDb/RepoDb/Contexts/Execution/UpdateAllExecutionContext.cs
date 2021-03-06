﻿using System;
using System.Collections.Generic;
using System.Data.Common;

namespace RepoDb.Contexts.Execution
{
    /// <summary>
    /// An execution context class used by update operation.
    /// </summary>
    /// <typeparam name="TEntity">The type of the data entity.</typeparam>
    internal class UpdateAllExecutionContext<TEntity>
        where TEntity : class
    {
        /// <summary>
        /// The execution command text.
        /// </summary>
        public string CommandText { get; set; }

        /// <summary>
        /// The list of the input <see cref="DbField"/> objects to be included in the execution.
        /// </summary>
        public IEnumerable<DbField> InputFields { get; set; }

        /// <summary>
        /// The compiled function that is used to set the <see cref="DbCommand"/> parameters.
        /// </summary>
        public Action<DbCommand, TEntity> SingleDataEntityParametersSetterFunc { get; set; }

        /// <summary>
        /// The compiled function that is used to set the <see cref="DbCommand"/> parameters.
        /// </summary>
        public Action<DbCommand, IList<TEntity>> MultipleDataEntitiesParametersSetterFunc { get; set; }
    }
}
