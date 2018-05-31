using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using Xunit;
using Assert = Xunit.Assert;

namespace DevExtreme.AspNet.Data.Tests.EF6 {

    class Bug_FindInList_DataItemA {
        public int ID { get; set; }
        public DateTime? Date { get; set; }
        public string Text { get; set; }
        public List<Bug_FindInList_DataItemB> Items { get; set; }
    }

    class Bug_FindInList_DataItemB {
        public int Id { get; set; }
        public string Name { get; set; }
    }

    partial class TestDbContext {
        public DbSet<Bug_FindInList_DataItemA> Bug_FindInList_ItemA { get; set; }
        public DbSet<Bug_FindInList_DataItemB> Bug_FindInList_ItemB { get; set; }
    }

    public class Bug_FindInList {

        [Fact]
        public void Scenario() {

            TestDbContext.Exec(context => {
                var dbSet = context.Bug_FindInList_ItemA;

                dbSet.AddRange(new[] {
                    new Bug_FindInList_DataItemA { ID = 1, Date = DateTime.Now, Text = "qqqq1",
                        Items = new List<Bug_FindInList_DataItemB> {
                            new Bug_FindInList_DataItemB {Id = 1, Name = "b_0011"},
                            new Bug_FindInList_DataItemB {Id = 2, Name = "b_0012"},
                            new Bug_FindInList_DataItemB {Id = 3, Name = "b_0013"}
                        }
                    },
                    new Bug_FindInList_DataItemA { ID = 2, Date = DateTime.Now, Text = "qqqq2",
                        Items = new List<Bug_FindInList_DataItemB> {
                            new Bug_FindInList_DataItemB {Id = 21, Name = "b_0021"},
                            new Bug_FindInList_DataItemB {Id = 22, Name = "b_0022"},
                            new Bug_FindInList_DataItemB {Id = 23, Name = "b_0023"}
                        }
                    },
                    new Bug_FindInList_DataItemA { ID = 3, Date = DateTime.Now, Text = "qqqq3",
                        Items = new List<Bug_FindInList_DataItemB> {
                            new Bug_FindInList_DataItemB {Id = 31, Name = "b_0031"},
                            new Bug_FindInList_DataItemB {Id = 32, Name = "b_0032"},
                            new Bug_FindInList_DataItemB {Id = 33, Name = "b_0033"}
                        }
                    }
                });

                context.SaveChanges();

                // we implement "any" operator for search in collections
                var filter = new List<object> { "Items", "any", new List<object> { "Id", ">", "1" } };
                var loadResult = DataSourceLoader.Load(dbSet, new SampleLoadOptions {
                    Filter = filter
                });

                /*// 2nd variant: by prepare expression
                    var queryableData = objectSpace.GetObjectsQuery<ServiceDeskRequest>();
                    var parameter = Expression.Parameter(typeof(ServiceDeskRequest), "A");
                    var expression = GetNavigationPropertyExpression(parameter, someUserOid, "Group", "Individuals", "Oid");
                    var whereCallExpression = Expression.Call(
                        typeof(Queryable), "Where", new Type[] { queryableData.ElementType }, queryableData.Expression, expression);
                    var results = queryableData.Provider.CreateQuery<ServiceDeskRequest>(whereCallExpression).Take(10);
                    foreach (ServiceDeskRequest item in results)
                        Console.WriteLine(item);*/

                var data = (IEnumerable<Bug_FindInList_DataItemA>)loadResult.data;
                var dataCount = data.Count();
                Assert.Equal(2, dataCount);
            });
        }
    }
}
