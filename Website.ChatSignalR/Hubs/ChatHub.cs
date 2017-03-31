using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Web;
using Microsoft.AspNet.SignalR;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Website.ChatSignalR.Models;
using Website.ChatSignalR.Controllers;

namespace Website.ChatSignalR.Hubs
{
    public class ChatHub : Hub
    {
        static ConcurrentDictionary<string, string> dic = new ConcurrentDictionary<string, string>();

        static ConcurrentDictionary<long, Customer> dictOnlineCustomers = new ConcurrentDictionary<long, Customer>();
        long _currentCustomerId;

        public void Hello()
        {
            Clients.All.hello();
        }

        //public void Send(string name, string message)
        //{
        //    Clients.All.broadcastMessage(name, message);
        //}

        public void SendToSpecific(string name, string message, long to)
        {
            //string toConnectionId = dic[to];
            string toConnectionId = GetActivedConnectionIdByCustomerId(to);

            // broadcast messae to caller
            Clients.Caller.broadcastMessage(name, message);

            // broadcast message to dest customer
            Clients.Client(toConnectionId).broadcastMessage(name, message);

            // save message to conversation
            var currentCustomer = GetCurrentCustomerByConnectionId(Context.ConnectionId);
            SaveMessageToConversation(currentCustomer.Id, to, message);
        }

        private void SaveMessageToConversation(long customerId, long toCustomerId, string message)
        {
            using (var db = new ChatSignalrContext())
            {
                var ids = new long[] { customerId, toCustomerId };
                // get old conversation between 2 customers
                var conv = db.Conversations
                    .Include(i => i.Customers)
                    .FirstOrDefault(x => x.Customers.Count == 2 && x.Customers.Any(n => ids.Contains(n.Id)));

                var newMsg = new Message
                {
                    Content = message,
                    DateCreated = DateTime.Now,
                    Customer = db.Customers.Find(customerId)
                };

                if (conv == null)
                {
                    conv = new Conversation();
                    conv.Customers.Add(db.Customers.Find(customerId));
                    conv.Customers.Add(db.Customers.Find(toCustomerId));
                    conv.FirstMessage = newMsg;
                    db.Conversations.Add(conv);
                    db.SaveChanges();
                }

                conv.Messages.Add(newMsg);

                conv.LastMessage = conv.Messages.Last();
                db.Entry(conv).State = EntityState.Modified;
                db.SaveChanges();
            }
        }

        private string GetActivedConnectionIdByCustomerId(long to)
        {
            return dictOnlineCustomers[to]
                .Connections
                .First(x => x.Connected)
                .ConnectionId;
        }

        private Customer GetCurrentCustomerByConnectionId(string connectionId)
        {
            return dictOnlineCustomers
                .FirstOrDefault(n => n.Value.Connections.Any(n2 => n2.ConnectionId == connectionId))
                .Value;
        }

        //public void Notify() //  public void Notify(string name, string id)
        //{
        //    var customerId = GetCookieCustomerId();
        //    if (dictOnlineCustomers.ContainsKey(customerId))
        //    {
        //        Clients.Caller.differentName();
        //    }
        //    else
        //    {
        //        dictOnlineCustomers.TryAdd(customerId, id);
        //        foreach (KeyValuePair<String, String> entry in dic)
        //        {
        //            Clients.Caller.online(entry.Key);
        //        }
        //        Clients.Others.enters(name);
        //    }
        //}

        public override Task OnConnected()
        {
            // #1 check valid customerid (ChatRoom/SignalrChat is already checked & prepared)
            Cookie s;
            int customerId = 0;
            if (!Context.Request.Cookies.TryGetValue(AppConstants.COOKIE_CHAT_CUSTOMERID, out s) // neu ko ton tai cookie
                || !int.TryParse(s.Value, out customerId)   // neu ko parse dc customerId
                || !CheckExisted(customerId))    // neu customer ko ton tai trong db
                throw new Exception("Need to check valid cookie " + AppConstants.COOKIE_CHAT_CUSTOMERID + " in action ChatRoom/SignalRChat");

            // #1.1 set global currentCustomerId
            _currentCustomerId = customerId;

            using (var db = new ChatSignalrContext())
            {
                var customer = db.Customers
                    .Include(i => i.Connections)
                    .FirstOrDefault(x => x.Id == customerId);

                // #2 update old customer connections, set connected = false
                foreach (var itm in customer.Connections.ToList())
                {
                    itm.Connected = false;
                    db.Entry(itm).State = EntityState.Modified;
                    db.SaveChanges();
                }

                // #3 update new customer connection to current customer
                customer.Connections.Add(new Connection
                {
                    Connected = true,
                    UserAgent = Context.Request.Headers["User-Agent"],
                    ConnectionId = Context.ConnectionId
                });

                db.Entry(customer).State = EntityState.Modified;
                db.SaveChanges();

                // #4 update onlines list to dict
                if (dictOnlineCustomers.ContainsKey(customer.Id))
                    throw new Exception("This customer - " + customer.Id + " is online");
                if (!dictOnlineCustomers.TryAdd(customer.Id, customer))
                    throw new Exception("Add this customer " + customer.Id + " to dictOnlineCustomers is failed!");

                // #5 notify (update) onlines list to all current customers
                foreach (KeyValuePair<long, Customer> entry in dictOnlineCustomers)
                {
                    // load onlines in caller
                    Clients.Caller.online(entry.Key, entry.Value.Name);
                    //Clients.All.online(entry.Key, entry.Value.Name);
                }

                // notify other this customer have enter.
                Clients.Others.enters(customer.Id, customer.Name);
            }

            return base.OnConnected();
        }

        private bool CheckExisted(int customerId)
        {
            using (var db = new ChatSignalrContext())
            {
                return db.Customers.Any(n => n.Id == customerId);
            }
        }


        public override Task OnDisconnected(bool stopCalled)
        {
            //var name = dic.FirstOrDefault(x => x.Value == Context.ConnectionId.ToString());
            var item = dictOnlineCustomers.FirstOrDefault(x => x.Value.Connections.Any(n => n.ConnectionId == Context.ConnectionId));
            Customer s;
            if (!dictOnlineCustomers.TryRemove(item.Key, out s))
                throw new Exception("Customer " + item.Key + " remove failed!");

            return Clients.All.disconnected(item.Key, item.Value.Name);
            //base.OnDisconnected(stopCalled: true);
        }
    }
}