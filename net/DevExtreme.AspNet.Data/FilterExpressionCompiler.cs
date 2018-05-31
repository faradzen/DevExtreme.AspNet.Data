using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DevExtreme.AspNet.Data {

    class FilterExpressionCompiler<T> : ExpressionCompiler {
        const string
            CONTAINS = "contains",
            NOT_CONTAINS = "notcontains",
            STARTS_WITH = "startswith",
            ENDS_WITH = "endswith";

        private readonly FilterAggregateExpressionCompiler _aggregateCompiler = new FilterAggregateExpressionCompiler();

        public FilterExpressionCompiler(bool guardNulls)
            : base(guardNulls) {
        }

        /// <summary>
        /// main method for prepare expression from criteria
        /// </summary>
        /// <param name="criteriaJson"></param>
        /// <returns></returns>
        public LambdaExpression Compile(IList criteriaJson) {
            var dataItemExpr = CreateItemParam(typeof(T));
            return Expression.Lambda(CompileCore(dataItemExpr, criteriaJson), dataItemExpr);
        }

        Expression CompileCore(ParameterExpression dataItemExpr, IList criteriaJson) {
            if(IsCriteria(criteriaJson[0]))
                return CompileGroup(dataItemExpr, criteriaJson);

            if(IsUnary(criteriaJson)) {
                return CompileUnary(dataItemExpr, criteriaJson);
            }

            if(IsAggregate(criteriaJson))
                return CompileAggregate(dataItemExpr, criteriaJson);

            return CompileBinary(dataItemExpr, criteriaJson);
        }

        Expression CompileAggregate(ParameterExpression dataItemExpr, IList criteriaJson) {
            // var clientOperation = Convert.ToString(criteriaJson[1]).ToLower();
            var clientAccessor = Convert.ToString(criteriaJson[0]);
            var clientCriteria = criteriaJson[2];
            var clientCriteriaList = criteriaJson[2] as IList;

            if(clientCriteriaList == null || clientCriteriaList.Count != 3 && clientCriteriaList[1] != "=") {
                throw new NotImplementedException($"not implemented parsing for filters as: {clientCriteriaList}");
            }
            var navPath = clientAccessor.Split('.').ToList();
            var innerPath = ((string)clientCriteriaList[0]).Split('.').ToList();
            navPath.AddRange(innerPath);

            var accessorExpr = CompileAccessorExpression(dataItemExpr, Convert.ToString(clientCriteriaList[0]));            
            var keyOid = clientCriteriaList[2];

            try {
                keyOid = Utils.ConvertClientValue(keyOid, accessorExpr.Type);
            } catch {
                return Expression.Constant(false);
            }

            var expression = navPath.Count == 1
                ? _aggregateCompiler.GetNavigationPropertyExpression(dataItemExpr, keyOid, accessorExpr.Type, navPath[0])
                : navPath.Count == 2
                    ? _aggregateCompiler.GetNavigationPropertyExpression(dataItemExpr, keyOid, accessorExpr.Type, navPath[0], navPath[1])
                    : _aggregateCompiler.GetNavigationPropertyExpression(dataItemExpr, keyOid, accessorExpr.Type, navPath[0], navPath[1], navPath[2]);

            // GetNavigationPropertyExpression works only for NET 4 
            if(expression == null)
                return Expression.Equal(Expression.Constant(true), Expression.Constant(false));
            return expression;
        }

        Expression CompileBinary(ParameterExpression dataItemExpr, IList criteriaJson) {
            var hasExplicitOperation = criteriaJson.Count > 2;

            var clientAccessor = Convert.ToString(criteriaJson[0]);
            // if operator in binary is not set ["fieldName","value"] then set celintOperation as "="
            var clientOperation = hasExplicitOperation ? Convert.ToString(criteriaJson[1]).ToLower() : "=";
            var clientValue = criteriaJson[hasExplicitOperation ? 2 : 1];
            var isStringOperation = clientOperation == CONTAINS || clientOperation == NOT_CONTAINS || clientOperation == STARTS_WITH || clientOperation == ENDS_WITH;

            var accessorExpr = CompileAccessorExpression(dataItemExpr, clientAccessor, progression => {
                if(isStringOperation)
                    ForceToString(progression);
            });

            if(isStringOperation) {
                return CompileStringFunction(accessorExpr, clientOperation, Convert.ToString(clientValue));

            } else {
                var useDynamicBinding = accessorExpr.Type == typeof(Object);
                var expressionType = TranslateBinaryOperation(clientOperation);

                if(!useDynamicBinding) {
                    try {
                        clientValue = Utils.ConvertClientValue(clientValue, accessorExpr.Type);
                    } catch {
                        return Expression.Constant(false);
                    }
                }

                if(clientValue == null && !Utils.CanAssignNull(accessorExpr.Type)) {
                    switch(expressionType) {
                        case ExpressionType.NotEqual:
                            return Expression.Constant(true);

                        case ExpressionType.Equal:
                        case ExpressionType.GreaterThan:
                        case ExpressionType.GreaterThanOrEqual:
                        case ExpressionType.LessThan:
                        case ExpressionType.LessThanOrEqual:
                            return Expression.Constant(false);
                    }
                }

                Expression valueExpr = Expression.Constant(clientValue, accessorExpr.Type);

                if(accessorExpr.Type == typeof(String) && IsInequality(expressionType)) {
                    if(clientValue == null)
                        valueExpr = Expression.Constant(null, typeof(String));

                    var compareMethod = typeof(String).GetMethod(nameof(String.Compare), new[] { typeof(String), typeof(String) });
                    accessorExpr = Expression.Call(null, compareMethod, accessorExpr, valueExpr);
                    valueExpr = Expression.Constant(0);
                } else if(useDynamicBinding) {
                    accessorExpr = Expression.Call(typeof(Utils).GetMethod(nameof(Utils.DynamicCompare)), accessorExpr, valueExpr);
                    valueExpr = Expression.Constant(0);
                }

                return Expression.MakeBinary(expressionType, accessorExpr, valueExpr);
            }

        }

        /// <summary> it is comparision operation
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        bool IsInequality(ExpressionType type) {
            return type == ExpressionType.LessThan || type == ExpressionType.LessThanOrEqual || type == ExpressionType.GreaterThanOrEqual || type == ExpressionType.GreaterThan;
        }

        Expression CompileStringFunction(Expression accessorExpr, string clientOperation, string value) {
            if(value != null)
                value = value.ToLower();

            var invert = false;

            if(clientOperation == NOT_CONTAINS) {
                clientOperation = CONTAINS;
                invert = true;
            }

            if(GuardNulls)
                accessorExpr = Expression.Coalesce(accessorExpr, Expression.Constant(""));

            var toLowerMethod = typeof(String).GetMethod(nameof(String.ToLower), Type.EmptyTypes);
            var operationMethod = typeof(String).GetMethod(GetStringOperationMethodName(clientOperation), new[] { typeof(String) });

            Expression result = Expression.Call(
                Expression.Call(accessorExpr, toLowerMethod),
                operationMethod,
                Expression.Constant(value)
            );

            if(invert)
                result = Expression.Not(result);

            return result;
        }

        Expression CompileGroup(ParameterExpression dataItemExpr, IList criteriaJson) {
            var operands = new List<Expression>();
            var isAnd = true;
            var nextIsAnd = true;

            foreach(var item in criteriaJson) {
                var operandJson = item as IList;

                if(IsCriteria(operandJson)) {
                    if(operands.Count > 1 && isAnd != nextIsAnd)
                        throw new ArgumentException("Mixing of and/or is not allowed inside a single group");

                    isAnd = nextIsAnd;
                    operands.Add(CompileCore(dataItemExpr, operandJson));
                    nextIsAnd = true;
                } else {
                    nextIsAnd = Regex.IsMatch(Convert.ToString(item), "and|&", RegexOptions.IgnoreCase);
                }
            }

            Expression result = null;
            var op = isAnd ? ExpressionType.AndAlso : ExpressionType.OrElse;

            foreach(var operand in operands) {
                if(result == null)
                    result = operand;
                else
                    result = Expression.MakeBinary(op, result, operand);
            }

            return result;
        }

        Expression CompileUnary(ParameterExpression dataItemExpr, IList criteriaJson) {
            return Expression.Not(CompileCore(dataItemExpr, (IList)criteriaJson[1]));
        }

        ExpressionType TranslateBinaryOperation(string clientOperation) {
            switch(clientOperation) {
                case "=":
                    return ExpressionType.Equal;

                case "<>":
                    return ExpressionType.NotEqual;

                case ">":
                    return ExpressionType.GreaterThan;

                case ">=":
                    return ExpressionType.GreaterThanOrEqual;

                case "<":
                    return ExpressionType.LessThan;

                case "<=":
                    return ExpressionType.LessThanOrEqual;
            }

            throw new NotSupportedException();
        }

        bool IsCriteria(object item) {
            return item is IList && !(item is String);
        }

        bool IsAggregate(IList criteriaJson) {
            return criteriaJson[1] is string s && s == "any";
        }

        internal bool IsUnary(IList criteriaJson) {
            return Convert.ToString(criteriaJson[0]) == "!";
        }

        string GetStringOperationMethodName(string clientOperation) {
            if(clientOperation == STARTS_WITH)
                return nameof(String.StartsWith);

            if(clientOperation == ENDS_WITH)
                return nameof(String.EndsWith);

            return nameof(String.Contains);
        }
    }

}
