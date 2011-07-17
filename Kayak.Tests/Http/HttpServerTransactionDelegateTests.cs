using System;
using System.Collections.Generic;
using System.Text;
using Kayak.Http;
using NUnit.Framework;
using Kayak.Tests.Net;

namespace Kayak.Tests.Http
{
    [TestFixture]
    public class HttpServerTransactionDelegateTests
    {
        TxLoop loop;
        TxContext context;

        static DataBuffer CreateBody(string[] body)
        {
            var bodyBuffer = new DataBuffer();
            
            foreach (var b in body)
                bodyBuffer.Add(new ArraySegment<byte>(Encoding.ASCII.GetBytes(b)));

            return bodyBuffer;
        }

        IEnumerable<TxCallbacks> Permute(Req req)
        {
            // XXX would be nice to increase this to 6. would need to buffer data in the transaction.
            // currently user must connect immediately, or before client writes req body data,
            // else data is lost :(
            var connectUpperBound = req.Body == null ? 0 : 2; 
            var disconnectUpperBound = connectUpperBound == 0 ? -1 : 2;

            for (int i = 0; i < connectUpperBound; i++)
            {
                for (int j = i - 1; j < disconnectUpperBound; j++)
                {
                    for (int k = -1; k < 6; k++)
                    {
                        var callbacks = CreateCallbacks();
                        var hooks = callbacks.Item2;

                        if (i != -1)
                            hooks[i].Add(() => context.ConnectToRequestBody());

                        if (j != i - 1 && j > -1)
                            hooks[j].Add(() => context.CancelRequestBody());

                        if (k != -1)
                            hooks[k].Add(() => context.CloseConnection());

                        Console.WriteLine("i = {0}, j = {1}, k = {2}", i, j, k);

                        yield return callbacks.Item1;
                    }
                }
            }
        }

        Tuple<TxCallbacks, List<List<Action>>> CreateCallbacks()
        {
            var callins = new List<List<Action>>();

            for (int i = 0; i < 6; i++)
                callins.Add(new List<Action>());

            var callbacks = new TxCallbacks()
            {
                OnRequest = () => callins[0].ForEach(a => a()),
                PostOnRequest = () => callins[1].ForEach(a => a()),
                OnRequestData = () => callins[2].ForEach(a => a()),
                PostOnRequestData = () => callins[3].ForEach(a => a()),
                OnRequestDataEnd = () => callins[4].ForEach(a => a()),
                PostOnRequestEnd = () => callins[5].ForEach(a => a())
            };

            return Tuple.Create(callbacks, callins);
        }


        static Req CreateReq(bool expectContinue, string[] data, BodyOptions options)
        {
            var head = default(HttpRequestHead);
            head.Version = new Version(1, 0);
            head.Headers = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            if (expectContinue)
            {
                head.Version = new Version(1, 1);
                head.Headers["Expect"] = "100-continue";
            }
            
            DataBuffer bodyBuffer = null;
            if (data != null)
            {
                switch (options)
                {
                    case BodyOptions.NoHeaderIndications:
                        bodyBuffer = CreateBody(data);
                        break;
                    case BodyOptions.ContentLength:
                        bodyBuffer = CreateBody(data);
                        head.Headers["Content-Length"] = bodyBuffer.GetCount().ToString();
                        break;
                    case BodyOptions.TransferEncodingChunked:
                        bodyBuffer = CreateBody(data);
                        head.Headers["Transfer-Encoding"] = "chunked";
                        break;
                    case BodyOptions.TransferEncodingNotChunked:
                        bodyBuffer = CreateBody(data);
                        head.Headers["Transfer-Encoding"] = "asddfashjkfdsa";
                        break;
                }
            }

            return new Req()
            {
                Head = head,
                Body = bodyBuffer
            };
        }

        [Test]
        public void No_body()
        {
            var req = CreateReq(false, null, BodyOptions.None);

            foreach (var cbs in Permute(req))
            {
                context = new TxContext(cbs);
                loop = new TxLoop(context, req);
                loop.Drive(cbs, false);

                Assert.That(context.HasRequestBody(), Is.False);
                context.AssertMessageObserver();
                context.AssertRequestBody(req);
                context.AssertResponseDelegate(false);
            }
        }

        [Test]
        public void Some_body(
            [Values(true, false)]
            bool expectContinue,
            [Values(
                BodyOptions.ContentLength,
                BodyOptions.TransferEncodingChunked)] 
            BodyOptions options)
        {
            var req = CreateReq(expectContinue, new[] { "some", "body" }, options);

            foreach (var cbs in Permute(req))
            {
                context = new TxContext(cbs);
                loop = new TxLoop(context, req);
                loop.Drive(cbs, false);

                Assert.That(context.HasRequestBody(), Is.True);
                context.AssertMessageObserver();
                context.AssertRequestBody(req);
                context.AssertResponseDelegate(expectContinue);
            }
        }
    }
    class TxContext
    {
        MockResponseDelegate responseDelegate;

        // want to be able to assert on contents of request body, also that continue is called
        // if needed
        IDataProducer requestBody;


        // just a pass-though
        public bool ShouldKeepAlive;

        Action closeConnectionAction;

        IDisposable requestBodyDisposable;
        MockDataConsumer requestBodyConsumer;

        IDisposable messageObserverDisposable;
        MockObserver<IDataProducer> messageObserver;

        TxCallbacks callbacks;

        bool gotCloseConnection;

        public TxContext(TxCallbacks callbacks)
        {
            this.callbacks = callbacks;

            requestBodyConsumer = new MockDataConsumer();

            if (callbacks.OnRequestData != null)
                requestBodyConsumer.OnDataAction = _ => callbacks.OnRequestData();

            if (callbacks.OnRequestDataEnd != null)
                requestBodyConsumer.OnEndAction = () => callbacks.OnRequestDataEnd();
        }

        public HttpServerTransactionDelegate CreateTransaction()
        {
            responseDelegate = new MockResponseDelegate();
            var tx = new HttpServerTransactionDelegate(
                new MockResponseFactory()
                {
                    OnCreate = (head, shouldKeepAlive, end) =>
                    {
                        closeConnectionAction = end;
                        ShouldKeepAlive = shouldKeepAlive;
                        return responseDelegate;
                    }
                },
                new MockRequestDelegate()
                {
                    OnRequestAction = (head, body, response) =>
                    {
                        Assert.That(response, Is.SameAs(responseDelegate));

                        requestBody = body;

                        if (callbacks.OnRequest != null)
                            callbacks.OnRequest();
                    }
                });

            messageObserver = new MockObserver<IDataProducer>();
            messageObserverDisposable = tx.Subscribe(messageObserver);

            return tx;
        }

        public bool HasRequestBody()
        {
            return requestBody != null;
        }

        // want to make sure that this causes OnComplete on the observer
        public void CloseConnection()
        {
            gotCloseConnection = true;
            closeConnectionAction();
        }

        public void CancelRequestBody()
        {
            requestBodyDisposable.Dispose();
        }

        public void ConnectToRequestBody()
        {
            requestBodyDisposable = requestBody.Connect(requestBodyConsumer);
        }

        public void AssertMessageObserver()
        {
            Assert.That(messageObserver.Values.Count, Is.EqualTo(1));
            Assert.That(messageObserver.Values[0], Is.SameAs(responseDelegate));
            Assert.That(messageObserver.Error, Is.Null);
            Assert.That(messageObserver.GotCompleted, Is.EqualTo(gotCloseConnection));
        }

        public void AssertRequestBody(Req req)
        {
            if (req.Body == null)
                Assert.That(requestBody, Is.Null);
            else
            {
                Assert.That(requestBodyConsumer.Buffer.GetString(), Is.EqualTo(req.Body.GetString()));
                Assert.That(requestBodyConsumer.GotEnd, Is.True);
            }
        }

        public void AssertResponseDelegate(bool expectContinue)
        {
            Assert.That(responseDelegate.GotWriteContinue, Is.EqualTo(expectContinue));
        }
    }

    class TxLoop
    {
        Req req;
        TxContext context;

        public TxLoop(TxContext context, Req req)
        {
            this.context = context;
            this.req = req;
        }

        public void Drive(TxCallbacks callbacks, bool keepAlive)
        {
            var tx = context.CreateTransaction();

            tx.OnRequest(req.Head, keepAlive);

            if (callbacks.PostOnRequest != null)
                callbacks.PostOnRequest();

            if (req.Body != null)
                req.Body.Each(data =>
                {
                    tx.OnRequestData(new ArraySegment<byte>(data), null);

                    if (callbacks.PostOnRequestData != null)
                        callbacks.PostOnRequestData();
                });

            tx.OnRequestEnd();

            if (callbacks.PostOnRequestEnd != null)
                callbacks.PostOnRequestEnd();
        }
    }

    public class Req
    {
        public HttpRequestHead Head;
        public DataBuffer Body;
    }

    class TxCallbacks
    {
        public Action OnRequest;
        public Action PostOnRequest;
        public Action OnRequestData;
        public Action PostOnRequestData;
        public Action OnRequestDataEnd;
        public Action PostOnRequestEnd;
    }

    class ReqBody
    {
        BodyOptions Options;
        public DataBuffer Data;
    }

    public enum BodyOptions
    {
        None,
        NoHeaderIndications,
        TransferEncodingChunked,
        TransferEncodingNotChunked,
        ContentLength
    }
}
