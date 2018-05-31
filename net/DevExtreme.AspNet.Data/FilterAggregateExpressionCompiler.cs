using System;
using System.Collections;
using System.Linq;
using System.Linq.Expressions;

namespace DevExtreme.AspNet.Data {

    /// <summary>
    /// good example and timesaver :)
    /// https://stackoverflow.com/questions/22672050/dynamic-expression-tree-to-filter-on-nested-collection-properties
    /// </summary>
    internal class FilterAggregateExpressionCompiler {
        /// <summary>
        /// works only for search like Group.Individuals.Any(Oid="...") 
        /// GetNavigationPropertyExpression(parameter, new Guid(""), "Group", "Individuals", "Oid") 
        /// </summary>
        /// <param name="parameter"></param>
        /// <param name="oid">key value for find</param>
        /// <param name="oidType"></param>
        /// <param name="properties"></param>
        /// <returns></returns>
        public Expression GetNavigationPropertyExpression(Expression parameter, object oid, Type oidType, params string[] properties) {
            Expression resultExpression = null;
            Expression navigationPropertyPredicate;
            Type childType = null;

#if(NET40 || NET45 || NET451 || NET452 || NET46 || NET461 || NET462 || NET47 || NET471 || NET472)

            if(properties.Count() > 1) {
                //build path
                parameter = Expression.Property(parameter, properties[0]);
                var isCollection = typeof(IEnumerable).IsAssignableFrom(parameter.Type);
                //if it´s a collection we later need to use the predicate in the methodexpressioncall
                Expression childParameter;
                if(isCollection) {
                    childType = parameter.Type.GetGenericArguments()[0];
                    childParameter = Expression.Parameter(childType, childType.Name);
                } else {
                    childParameter = parameter;
                }
                //skip current property and get navigation property expression recursivly
                var innerProperties = properties.Skip(1).ToArray();
                navigationPropertyPredicate = GetNavigationPropertyExpression(childParameter, oid, oidType, innerProperties);
                if(isCollection) {
                    //build methodexpressioncall
                    var anyMethod = typeof(Enumerable).GetMethods().Single(m => m.Name == "Any" && m.GetParameters().Length == 2);
                    anyMethod = anyMethod.MakeGenericMethod(childType);
                    navigationPropertyPredicate = Expression.Call(anyMethod, parameter, navigationPropertyPredicate);
                    resultExpression = MakeLambda(parameter, navigationPropertyPredicate);
                } else {
                    resultExpression = navigationPropertyPredicate;
                }
            } else {
                //Formerly from ACLAttribute
                var childProperty = parameter.Type.GetProperty(properties[0]);

                var left = Expression.Property(parameter, childProperty);
                var right = Expression.Constant(oid, oidType);
                navigationPropertyPredicate = Expression.Equal(left, right);

                resultExpression = MakeLambda(parameter, navigationPropertyPredicate);
            }
            return resultExpression;

#endif

            return null;
        }

        private Expression MakeLambda(Expression parameter, Expression predicate) {
            var resultParameterVisitor = new ParameterVisitor();
            resultParameterVisitor.Visit(parameter);
            var resultParameter = resultParameterVisitor.Parameter;
            return Expression.Lambda(predicate, (ParameterExpression)resultParameter);
        }

        private class ParameterVisitor : ExpressionVisitor {
            public Expression Parameter {
                get;
                private set;
            }
            protected override Expression VisitParameter(ParameterExpression node) {
                Parameter = node;
                return node;
            }
        }
    }
}
