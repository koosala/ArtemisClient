﻿using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Amqp;
using Amqp.Framing;
using Amqp.Listener;

namespace ArtemisLib2
{
    public class LinkProcessor : ILinkProcessor
    {
        SharedLinkEndpoint sharedLinkEndpoint = new SharedLinkEndpoint();

        public void Setup()
        {
            string address = "amqp://admin:admin@127.0.0.1:5672";

            Uri addressUri = new Uri(address);
            ContainerHost host = new ContainerHost(new[] {addressUri}, null, addressUri.UserInfo);
            host.Open(); // Throws socketexception - AddressAlreadyInUse - only one usage of each socket address (protocol/network address/port) is normally permitted
            host.RegisterLinkProcessor(new LinkProcessor());
        }

        public void Process(AttachContext attachContext)
        {
            var task = this.ProcessAsync(attachContext);
        }

        async Task ProcessAsync(AttachContext attachContext)
        {
            // simulating an async operation required to complete the task
            await Task.Delay(100);

            if (attachContext.Attach.LinkName == "")
            {
                // how to fail the attach request
                attachContext.Complete(
                    new Error(ErrorCode.InvalidField) {Description = "Empty link name not allowed."});
            }
            else if (attachContext.Link.Role)
            {
                var target = attachContext.Attach.Target as Target;
                if (target != null)
                {
                    if (target.Address == "slow-queue")
                    {
                        // how to do manual link flow control
                        new SlowLinkEndpoint(attachContext);
                    }
                    else if (target.Address == "shared-queue")
                    {
                        // how to do flow control across links
                        this.sharedLinkEndpoint.AttachLink(attachContext);
                    }
                    else
                    {
                        // default link flow control
                        attachContext.Complete(new IncomingLinkEndpoint(), 300);
                    }
                }
            }
            else
            {
                attachContext.Complete(new OutgoingLinkEndpoint(), 0);
            }
        }

        class SlowLinkEndpoint : LinkEndpoint
        {
            ListenerLink link;
            CancellationTokenSource cts;

            public SlowLinkEndpoint(AttachContext attachContext)
            {
                this.link = attachContext.Link;
                this.cts = new CancellationTokenSource();
                link.Closed += (o, e) => this.cts.Cancel();
                attachContext.Complete(this, 0);
                this.link.SetCredit(1, false, false);
            }

            public override void OnMessage(MessageContext messageContext)
            {
                messageContext.Complete();

                // delay 1s for the next message
                Task.Delay(1000, this.cts.Token).ContinueWith(
                    t =>
                    {
                        if (!t.IsCanceled)
                        {
                            this.link.SetCredit(1, false, false);
                        }
                    });
            }

            public override void OnFlow(FlowContext flowContext)
            {
            }

            public override void OnDisposition(DispositionContext dispositionContext)
            {
            }
        }

        class SharedLinkEndpoint : LinkEndpoint
        {
            const int Capacity = 5; // max concurrent request
            SemaphoreSlim semaphore;
            ConcurrentDictionary<Link, CancellationTokenSource> ctsMap;

            public SharedLinkEndpoint()
            {
                this.semaphore = new SemaphoreSlim(Capacity);
                this.ctsMap = new ConcurrentDictionary<Link, CancellationTokenSource>();
            }

            public void AttachLink(AttachContext attachContext)
            {
                this.semaphore.WaitAsync(30000).ContinueWith(
                    t =>
                    {
                        if (!t.Result)
                        {
                            attachContext.Complete(new Error(ErrorCode.ResourceLimitExceeded));
                        }
                        else
                        {
                            this.semaphore.Release();
                            attachContext.Complete(this, 1);
                        }
                    });
            }

            public override void OnMessage(MessageContext messageContext)
            {
                this.semaphore.WaitAsync(30000).ContinueWith(
                    t =>
                    {
                        if (!t.Result)
                        {
                            messageContext.Complete(new Error(ErrorCode.ResourceLimitExceeded));
                        }
                        else
                        {
                            this.semaphore.Release();
                            messageContext.Complete();
                        }
                    });
            }

            public override void OnFlow(FlowContext flowContext)
            {
            }

            public override void OnDisposition(DispositionContext dispositionContext)
            {
            }
        }

        class IncomingLinkEndpoint : LinkEndpoint
        {
            public override void OnMessage(MessageContext messageContext)
            {
                // this can also be done when an async operation, if required, is done
                messageContext.Complete();
            }

            public override void OnFlow(FlowContext flowContext)
            {
            }

            public override void OnDisposition(DispositionContext dispositionContext)
            {
            }
        }

        class OutgoingLinkEndpoint : LinkEndpoint
        {
            long id;

            public override void OnFlow(FlowContext flowContext)
            {
                for (int i = 0; i < flowContext.Messages; i++)
                {
                    var message = new Message("Hello!");
                    message.Properties = new Properties() {Subject = "Message" + Interlocked.Increment(ref this.id)};
                    flowContext.Link.SendMessage(message);
                }
            }

            public override void OnDisposition(DispositionContext dispositionContext)
            {
                if (!(dispositionContext.DeliveryState is Accepted))
                {
                    // Handle the case where message is not accepted by the client
                }

                dispositionContext.Complete();
            }
        }
    }
}