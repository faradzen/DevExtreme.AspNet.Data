using DevExtreme.AspNet.Data.RemoteGrouping;
using System.Linq;
using System.Linq.Expressions;
// need for netstandart case
using System.Reflection;

namespace DevExtreme.AspNet.Data {

    class DataSourceExpressionBuilder<T> {
        DataSourceLoadOptionsBase _loadOptions;
        bool _guardNulls;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="loadOptions"></param>
        /// <param name="guardNulls">i think this param is smth like this: because we have already enumerated data source (linq to objects) then we need guard nulls... (wtf?) </param>
        public DataSourceExpressionBuilder(DataSourceLoadOptionsBase loadOptions, bool guardNulls) {
            _loadOptions = loadOptions;
            _guardNulls = guardNulls;
        }

        public Expression BuildLoadExpr(Expression source, bool paginate = true) {
            return BuildCore(source, paginate: paginate);
        }

        public Expression BuildCountExpr(Expression source) {
            return BuildCore(source, isCountQuery: true);
        }

        public Expression BuildLoadGroupsExpr(Expression source) {
            return BuildCore(source, remoteGrouping: true);
        }

        /// <summary>
        /// core build query function. start point
        /// </summary>
        /// <param name="expr"></param>
        /// <param name="paginate"></param>
        /// <param name="isCountQuery"></param>
        /// <param name="remoteGrouping"></param>
        /// <returns></returns>
        Expression BuildCore(Expression expr, bool paginate = false, bool isCountQuery = false, bool remoteGrouping = false) {
            var queryableType = typeof(Queryable);
            var genericTypeArguments = new[] { typeof(T) };

            // call expression with filter
            if(_loadOptions.HasFilter)
                expr = Expression.Call(queryableType, "Where", genericTypeArguments, expr, Expression.Quote(new FilterExpressionCompiler<T>(_guardNulls).Compile(_loadOptions.Filter)));

            if(!isCountQuery) {
                if(!remoteGrouping) {
                    if(_loadOptions.HasAnySort)
                        expr = new SortExpressionCompiler<T>(_guardNulls).Compile(expr, _loadOptions.GetFullSort());
                    if(_loadOptions.HasAnySelect && _loadOptions.UseRemoteSelect) {
                        expr = new SelectExpressionCompiler<T>(_guardNulls).Compile(expr, _loadOptions.GetFullSelect());
#if(NET40)
                        genericTypeArguments = expr.Type.GetGenericArguments();
#elif(NETSTANDARD1_0 || NETSTANDARD1_1 || NETSTANDARD1_2 || NETSTANDARD1_3 || NETSTANDARD1_4 || NETSTANDARD1_5 || NETSTANDARD1_6 || NETSTANDARD2_0)
                        genericTypeArguments = expr.Type.GetTypeInfo().IsGenericTypeDefinition ? expr.Type.GetTypeInfo().GenericTypeParameters : expr.Type.GetTypeInfo().GenericTypeArguments;
#elif(NETCOREAPP1_0 ||  NETCOREAPP1_1 || NETCOREAPP2_0 || NETCOREAPP2_1)
                        IEnumerable<Type> arguments = expr.Type.IsGenericTypeDefinition 
                                ? expr.Type.GenericTypeParameters
                                : expr.Type.GenericTypeArguments;
                        genericTypeArguments = arguments.Select(x => x.GetTypeInfo()).ToArray();
#endif
                    }
                } else {
                    expr = new RemoteGroupExpressionCompiler<T>(_guardNulls, _loadOptions.Group, _loadOptions.TotalSummary, _loadOptions.GroupSummary).Compile(expr);
                }

                if(paginate) {
                    if(_loadOptions.Skip > 0)
                        expr = Expression.Call(queryableType, "Skip", genericTypeArguments, expr, Expression.Constant(_loadOptions.Skip));

                    if(_loadOptions.Take > 0)
                        expr = Expression.Call(queryableType, "Take", genericTypeArguments, expr, Expression.Constant(_loadOptions.Take));
                }
            }

            if(isCountQuery)
                expr = Expression.Call(queryableType, "Count", genericTypeArguments, expr);

            return expr;
        }
    }

}
