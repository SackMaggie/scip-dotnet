  namespace Main;
//          ^^^^ reference scip-dotnet nuget . . Main/

  public static class GenericNames
//                    ^^^^^^^^^^^^ definition scip-dotnet nuget . . Main/GenericNames#
//                                 documentation ```cs\nclass GenericNames\n```
  {
      public static T Create<T>() where T : new() => new T();
//                  ^ reference local 0
//                    ^^^^^^ definition scip-dotnet nuget . . Main/GenericNames#Create().
//                           documentation ```cs\npublic static T GenericNames.Create<T>() where T : new()\n```
//                           ^ definition local 0
//                             documentation ```cs\nT\n```
//                                      ^ reference local 0
//                                                       ^ reference local 0
      public static T Create<T>(T seed) => seed;
//                  ^ reference local 1
//                    ^^^^^^ definition scip-dotnet nuget . . Main/GenericNames#Create(+1).
//                           documentation ```cs\npublic static T GenericNames.Create<T>(T seed)\n```
//                           ^ definition local 1
//                             documentation ```cs\nT\n```
//                              ^ reference local 1
//                                ^^^^ definition scip-dotnet nuget . . Main/GenericNames#Create(+1).(seed)
//                                     documentation ```cs\nT seed\n```
//                                         ^^^^ reference scip-dotnet nuget . . Main/GenericNames#Create(+1).(seed)
      public static TOut Convert<TIn, TOut>(TIn value, Func<TIn, TOut> convert) => convert(value);
//                  ^^^^ reference local 2
//                       ^^^^^^^ definition scip-dotnet nuget . . Main/GenericNames#Convert().
//                               documentation ```cs\npublic static TOut GenericNames.Convert<TIn, TOut>(TIn value, Func<TIn, TOut> convert)\n```
//                               ^^^ definition local 3
//                                   documentation ```cs\nTIn\n```
//                                    ^^^^ definition local 2
//                                         documentation ```cs\nTOut\n```
//                                          ^^^ reference local 3
//                                              ^^^^^ definition scip-dotnet nuget . . Main/GenericNames#Convert().(value)
//                                                    documentation ```cs\nTIn value\n```
//                                                     ^^^^ reference scip-dotnet nuget System.Runtime 10.0.0.0 System/Func#
//                                                          ^^^ reference local 3
//                                                               ^^^^ reference local 2
//                                                                     ^^^^^^^ definition scip-dotnet nuget . . Main/GenericNames#Convert().(convert)
//                                                                             documentation ```cs\nFunc<TIn, TOut> convert\n```
//                                                                                 ^^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#Convert().(convert)
//                                                                                         ^^^^^ reference scip-dotnet nuget . . Main/GenericNames#Convert().(value)
  }

  public static class GenericNameExtensions
//                    ^^^^^^^^^^^^^^^^^^^^^ definition scip-dotnet nuget . . Main/GenericNameExtensions#
//                                          documentation ```cs\nclass GenericNameExtensions\n```
  {
      public static T As<T>(this object value) where T : class => (T)value;
//                  ^ reference local 4
//                    ^^ definition scip-dotnet nuget . . Main/GenericNameExtensions#As().
//                       documentation ```cs\npublic T object.As<T>() where T : class\n```
//                       ^ definition local 4
//                         documentation ```cs\nT\n```
//                                      ^^^^^ definition scip-dotnet nuget . . Main/GenericNameExtensions#As().(value)
//                                            documentation ```cs\nobject value\n```
//                                                   ^ reference local 4
//                                                                 ^ reference local 4
//                                                                   ^^^^^ reference scip-dotnet nuget . . Main/GenericNameExtensions#As().(value)
  }

  public class GenericNameUsage
//             ^^^^^^^^^^^^^^^^ definition scip-dotnet nuget . . Main/GenericNameUsage#
//                              documentation ```cs\nclass GenericNameUsage\n```
  {
      private readonly List<int> _items = new List<int>();
//                     ^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/List#
//                               ^^^^^^ definition scip-dotnet nuget . . Main/GenericNameUsage#_items.
//                                      documentation ```cs\nprivate readonly List<int> GenericNameUsage._items\n```
//                                            ^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/List#
      private readonly Dictionary<string, List<int>> _index = new Dictionary<string, List<int>>();
//                     ^^^^^^^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/Dictionary#
//                                        ^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/List#
//                                                   ^^^^^^ definition scip-dotnet nuget . . Main/GenericNameUsage#_index.
//                                                          documentation ```cs\nprivate readonly Dictionary<string, List<int>> GenericNameUsage._index\n```
//                                                                ^^^^^^^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/Dictionary#
//                                                                                   ^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/List#

      public void Invoke()
//                ^^^^^^ definition scip-dotnet nuget . . Main/GenericNameUsage#Invoke().
//                       documentation ```cs\npublic void GenericNameUsage.Invoke()\n```
      {
          var created = GenericNames.Create<GenericNameUsage>();
//            ^^^^^^^ definition local 5
//                    documentation ```cs\nGenericNameUsage? created\n```
//                      ^^^^^^^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#
//                                   ^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#Create().
//                                          ^^^^^^^^^^^^^^^^ reference scip-dotnet nuget . . Main/GenericNameUsage#
          var seeded = GenericNames.Create<int>(1);
//            ^^^^^^ definition local 6
//                   documentation ```cs\nint seeded\n```
//                     ^^^^^^^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#
//                                  ^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#Create(+1).
          var converted = GenericNames.Convert<int, string>(seeded, value => value.ToString());
//            ^^^^^^^^^ definition local 7
//                      documentation ```cs\nstring? converted\n```
//                        ^^^^^^^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#
//                                     ^^^^^^^ reference scip-dotnet nuget . . Main/GenericNames#Convert().
//                                                          ^^^^^^ reference local 6
//                                                                  ^^^^^ definition local 9
//                                                                        documentation ```cs\nint value\n```
//                                                                           ^^^^^ reference local 9
//                                                                                 ^^^^^^^^ reference scip-dotnet nuget System.Runtime 10.0.0.0 System/Int32#ToString().
          object boxed = this;
//               ^^^^^ definition local 10
//                     documentation ```cs\nobject boxed\n```
          var unboxed = boxed.As<GenericNameUsage>();
//            ^^^^^^^ definition local 11
//                    documentation ```cs\nGenericNameUsage? unboxed\n```
//                      ^^^^^ reference local 10
//                            ^^ reference scip-dotnet nuget . . Main/GenericNameExtensions#As().
//                               ^^^^^^^^^^^^^^^^ reference scip-dotnet nuget . . Main/GenericNameUsage#
          var ints = _items.OfType<int>().ToList();
//            ^^^^ definition local 12
//                 documentation ```cs\nList<int>? ints\n```
//                   ^^^^^^ reference scip-dotnet nuget . . Main/GenericNameUsage#_items.
//                          ^^^^^^ reference scip-dotnet nuget System.Linq 10.0.0.0 Linq/Enumerable#OfType().
//                                        ^^^^^^ reference scip-dotnet nuget System.Linq 10.0.0.0 Linq/Enumerable#ToList().
          var unbound = typeof(List<>);
//            ^^^^^^^ definition local 13
//                    documentation ```cs\nType? unbound\n```
//                             ^^^^ reference scip-dotnet nuget System.Collections 10.0.0.0 Generic/List#
          IEnumerable<int> sequence = _items;
//        ^^^^^^^^^^^ reference scip-dotnet nuget System.Runtime 10.0.0.0 Generic/IEnumerable#
//                         ^^^^^^^^ definition local 14
//                                  documentation ```cs\nIEnumerable<int> sequence\n```
//                                    ^^^^^^ reference scip-dotnet nuget . . Main/GenericNameUsage#_items.
          _index[converted] = ints;
//        ^^^^^^ reference scip-dotnet nuget . . Main/GenericNameUsage#_index.
//               ^^^^^^^^^ reference local 7
//                            ^^^^ reference local 12
      }
  }
