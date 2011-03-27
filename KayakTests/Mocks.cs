
namespace KayakTests
{
    static class Mocks
    {


        //public static void SetupCallbacks(this Mock<ITransaction> mockTransaction, IEnumerable<Action<IHttpTransactionHandler>> callbacks)
        //{
        //    var en = callbacks.GetEnumerator();

        //    mockTransaction
        //        .Setup(d => d.RunCallbacks(It.IsAny<IHttpTransactionHandler>()))
        //        .Returns<IHttpTransactionHandler>(h =>
        //        {
        //            if (en.MoveNext())
        //            {
        //                return (r, ex) =>
        //                    {
        //                        try
        //                        {
        //                            en.Current(h);
        //                            r(true);
        //                        }
        //                        catch (Exception e)
        //                        {
        //                            ex(e);
        //                        }
        //                    };
        //            }
        //            else
        //            {
        //                Console.WriteLine("Finished.");
        //                en.Dispose();
        //                return (r, ex) =>
        //                    {
        //                        r(false);
        //                    };
        //            }
        //        });
        //}
    }
}
