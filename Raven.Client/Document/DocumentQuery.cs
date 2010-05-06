﻿using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using Raven.Client.Client;
using Raven.Database.Data;
using System.Linq;

namespace Raven.Client.Document
{
	public class DocumentQuery<T> : AbstractDocumentQuery<T>
	{
		private readonly IDatabaseCommands databaseCommands;

	    public DocumentQuery(DocumentSession session, IDatabaseCommands databaseCommands, string indexName, string[] projectionFields):base(session)
		{
	    	this.databaseCommands = databaseCommands;
		    this.projectionFields = projectionFields;
		    this.indexName = indexName;
		}

	    public override IDocumentQuery<TProjection> Select<TProjection>(Func<T, TProjection> projectionExpression)
	    {
			return new DocumentQuery<TProjection>(session, databaseCommands, indexName,
	                                              projectionExpression
	                                                  .Method
	                                                  .ReturnType
	                                                  .GetProperties(BindingFlags.Instance | BindingFlags.Public)
	                                                  .Select(x => x.Name).ToArray()
	            )
	        {
	            pageSize = pageSize,
	            query = query,
	            start = start,
				timeout = timeout,
	            waitForNonStaleResults = waitForNonStaleResults
	        };
	    }

	    protected override QueryResult GetQueryResult()
		{
	    	var sp = Stopwatch.StartNew();
			while (true) 
			{
				Trace.WriteLine(string.Format("Executing query '{0}' on index '{1}' in '{2}'",
								query, indexName, session.StoreIdentifier));
				var result = databaseCommands.Query(indexName, new IndexQuery
				{
					Query = query,
					PageSize = pageSize,
					Start = start,
					SortedFields = orderByFields.Select(x => new SortedField(x)).ToArray(),
					FieldsToFetch = projectionFields
				});
				if(waitForNonStaleResults && result.IsStale)
				{
					if (sp.Elapsed > timeout)
					{
						sp.Stop();
						throw new TimeoutException(string.Format("Waited for {0:#,#}ms for the query to return non stale result.", sp.ElapsedMilliseconds));
					}
					Trace.WriteLine(
						string.Format("Stale query results on non stable query '{0}' on index '{1}' in '{2}', query will be retired",
						              query, indexName, session.StoreIdentifier));
					Thread.Sleep(100);
					continue;
				}
				Trace.WriteLine(string.Format("Query returned {0}/{1} results", result.Results.Length, result.TotalResults));
				return result;
			} 
		}
	}
}