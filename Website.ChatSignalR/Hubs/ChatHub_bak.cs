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

        #region Utilities

        private bool CheckExisted(int customerId)
        {
            using (var db = new ChatSignalrContext())
            {
                return db.Customers.Any(n => n.Id == customerId);
            }
        }

        private Customer GetCustomerById(long customerId)
        {
            using (var db = new ChatSignalrContext())
            {
                return db.Customers.FirstOrDefault(x => x.Id == customerId);
            }
        }

        private void SaveMessageToConversation(long customerId, long toCustomerId, string message)
        {
            using (var db = new ChatSignalrContext())
            {
                var ids = new long[] { customerId, toCustomerId };
                // get old conversation between 2 customers
                var conv = db.Conversations
                    .Include(i => i.Customers)
                    .FirstOrDefault(x => x.Customers.Count == 2 && x.Customers.All(n => ids.Contains(n.Id)));

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

        /// <summary>
        /// Get all active connections of current customer
        /// </summary>
        /// <param name="customerId"></param>
        /// <returns></returns>
        private string[] GetActiveCustomerConnections(long customerId)
        {
            return dictOnlineCustomers[customerId]
                .Connections
                .Where(x => x.Connected)
                .Select(s => s.ConnectionId)
                .ToArray();
        }

        private Customer GetCurrentCustomerByConnectionId(string connectionId)
        {
            return dictOnlineCustomers
                .FirstOrDefault(n => n.Value.Connections.Any(n2 => n2.ConnectionId == connectionId))
                .Value;
        }


        #endregion

        //public void SendToSpecific(string message, long toCustomerId)
        //{
        //    // get current customer by connection
        //    var customer = GetCurrentCustomerByConnectionId(Context.ConnectionId);

        //    //string toConnectionId = dic[to];
        //    string toConnectionId = GetActivedConnectionIdByCustomerId(toCustomerId);

        //    // broadcast messae to caller
        //    Clients.Caller.broadcastMessage(customer.Id, customer.Name, message);

        //    // broadcast message to dest customer
        //    Clients.Client(toConnectionId).broadcastMessage(customer.Id, customer.Name, message);

        //    // save message to conversation
        //    SaveMessageToConversation(customer.Id, toCustomerId, message);
        //}

        //public void SendToSpecific(string message, long toCustomerId)
        //{
        //    // get current customer by connection
        //    var customer = GetCurrentCustomerByConnectionId(Context.ConnectionId);

        //    // broadcast messae to caller
        //    Clients.Caller.broadcastMessage(customer.Id, customer.Name, message);

        //    // broadcast message to all dest customer connections // gui cho tat ca ket noi hien tai cua kh, tat ca cac tabs
        //    //string toConnectionId = GetActivedConnectionIdByCustomerId(toCustomerId);
        //    var toConnectionIds = GetActiveCustomerConnections(toCustomerId);
        //    foreach (var connId in toConnectionIds)
        //    {
        //        Clients.Client(connId).broadcastMessage(customer.Id, customer.Name, message);
        //    }

        //    // save message to conversation
        //    SaveMessageToConversation(customer.Id, toCustomerId, message);
        //}

        public void SendToSpecific(string message, long toUserId)
        {
            var curentUserId = GetCurrentUserId();
            using (var db = new ChatSignalrContext())
            {
                var user = db.Customers
                    .Include(i => i.Connections)
                    .FirstOrDefault(x=>x.Id == toUserId);

                var activeConnIds = user.Connections.Where(x => x.Connected)
                    .Select(s => s.ConnectionId)
                    .ToList();

                if (user == null)
                {
                    Clients.Caller.showErrorMessage("Could not find that user (" + toUserId + ")");
                }
                else
                {
                    if (activeConnIds.Count == 0)
                    {
                        // Thong bao nhung van gui va luu tin nhan vao db cho user nay doc
                        Clients.Caller.showErrorMessage("The user is no longer connected.");
                    }
                    else
                    {
                        // Gửi tin nhắn cho tất các các kết nối hiện tại của user // all user connections
                        Clients.Clients(activeConnIds).broadcastMessage(user.Id, user.Name, message);
                    }
                }
            }


            // get current customer by connection

            var customer = GetCurrentCustomerByConnectionId(Context.ConnectionId);

            // broadcast messae to caller
            Clients.Caller.broadcastMessage(customer.Id, customer.Name, message);

            // broadcast message to all dest customer connections // gui cho tat ca ket noi hien tai cua kh, tat ca cac tabs
            //string toConnectionId = GetActivedConnectionIdByCustomerId(toCustomerId);
            var toConnectionIds = GetActiveCustomerConnections(toUserId);
            foreach (var connId in toConnectionIds)
            {
                Clients.Client(connId).broadcastMessage(customer.Id, customer.Name, message);
            }

            // save message to conversation
            SaveMessageToConversation(customer.Id, toUserId, message);
        }

        /// <summary>
        /// Get current userid via from cookie data
        /// </summary>
        /// <returns></returns>
        private long GetCurrentUserId()
        {
            Cookie s;
            int customerId = 0;

            if (Context.Request.Cookies.TryGetValue(AppConstants.COOKIE_CHAT_CUSTOMERID, out s))
                int.TryParse(s.Value, out customerId);

            return customerId;
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

        //public override Task OnConnected()
        //{
        //    // #1 check valid customerid (ChatRoom/SignalrChat is already checked & prepared)
        //    Cookie s;
        //    int customerId = 0;
        //    if (!Context.Request.Cookies.TryGetValue(AppConstants.COOKIE_CHAT_CUSTOMERID, out s) // neu ko ton tai cookie
        //        || !int.TryParse(s.Value, out customerId)   // neu ko parse dc customerId
        //        || !CheckExisted(customerId))    // neu customer ko ton tai trong db
        //        throw new Exception("Need to check valid cookie " + AppConstants.COOKIE_CHAT_CUSTOMERID + " in action ChatRoom/SignalRChat");

        //    using (var db = new ChatSignalrContext())
        //    {
        //        var customer = db.Customers
        //            .Include(i => i.Connections)
        //            .FirstOrDefault(x => x.Id == customerId);

        //        // #2 update old customer connections, set connected = false
        //        foreach (var itm in customer.Connections.Where(x => !x.Connected).ToList())
        //        {
        //            itm.Connected = false;
        //            db.Entry(itm).State = EntityState.Modified;
        //            db.SaveChanges();
        //        }

        //        // #3 update new customer connection to current customer
        //        customer.Connections.Add(new Connection
        //        {
        //            Connected = true,
        //            UserAgent = Context.Request.Headers["User-Agent"],
        //            ConnectionId = Context.ConnectionId
        //        });

        //        db.Entry(customer).State = EntityState.Modified;
        //        db.SaveChanges();

        //        // #4 update onlines list to dict
        //        if (dictOnlineCustomers.ContainsKey(customer.Id))
        //        {
        //            /// truong hợp mở tab mới trong khi tab cũ chưa ngắt kết nối
        //            /// tạm thời ko xét trường hợp này
        //            // throw new Exception("This customer - " + customer.Id + " is online");
        //        }
        //        else
        //        {
        //            //add model only
        //            var customerModel = new Customer
        //            {
        //                Connections = customer.Connections.Where(x => x.Connected).ToList(),
        //                Id = customer.Id,
        //                Name = customer.Name,
        //            };

        //            if (!dictOnlineCustomers.TryAdd(customerModel.Id, customerModel))
        //                throw new Exception("Add this customer " + customerModel.Id + " to dictOnlineCustomers is failed!");
        //        }

        //        // #5 notify (update) onlines list to all current customers
        //        foreach (KeyValuePair<long, Customer> entry in dictOnlineCustomers)
        //        {
        //            // load onlines in caller
        //            string lastMessage = "";
        //            Clients.Caller.online(entry.Key, entry.Value.Name, lastMessage);
        //            //Clients.All.online(entry.Key, entry.Value.Name);
        //        }

        //        // notify other this customer have enter.
        //        Clients.Others.enters(customer.Id, customer.Name);
        //    }

        //    return base.OnConnected();
        //}

        public override Task OnConnected()
        {
            System.Threading.Thread.Sleep(4000);

            // #1 check valid customerid (ChatRoom/SignalrChat is already checked & prepared)
            Cookie s;
            int customerId = 0;
            if (!Context.Request.Cookies.TryGetValue(AppConstants.COOKIE_CHAT_CUSTOMERID, out s) // neu ko ton tai cookie
                || !int.TryParse(s.Value, out customerId)   // neu ko parse dc customerId
                || !CheckExisted(customerId))    // neu customer ko ton tai trong db
                throw new Exception("Need to check valid cookie " + AppConstants.COOKIE_CHAT_CUSTOMERID + " in action ChatRoom/SignalRChat");

            using (var db = new ChatSignalrContext())
            {
                var user = db.Customers
                    .Include(i => i.Connections)
                    .FirstOrDefault(x => x.Id == customerId);

                // #3 add connection to current user
                var newConn = new Connection
                {
                    Connected = true,
                    UserAgent = Context.Request.Headers["User-Agent"],
                    ConnectionId = Context.ConnectionId
                };

                user.Connections.Add(newConn);
                db.Entry(user).State = EntityState.Modified;
                db.SaveChanges();

                var newestActiveConns = user.Connections.Where(x => x.Connected).ToList();

                // #4 update onlines list to dict
                if (dictOnlineCustomers.ContainsKey(user.Id))
                {
                    // update connection to dictionary
                    //var item = dictOnlineCustomers.FirstOrDefault(x => x.Value.Connections.Any(n => n.ConnectionId == Context.ConnectionId));
                    var item = dictOnlineCustomers.FirstOrDefault(x => x.Key == user.Id);
                    item.Value.Connections.Clear();
                    item.Value.Connections = newestActiveConns; // kie tra lai de dam bao se cap nhat vao dict
                }
                else
                {
                    // add model only
                    var customerModel = new Customer
                    {
                        Connections = newestActiveConns,
                        Id = user.Id,
                        Name = user.Name,
                    };

                    if (!dictOnlineCustomers.TryAdd(customerModel.Id, customerModel))
                        throw new Exception("Add this customer " + customerModel.Id + " to dictOnlineCustomers is failed!");
                }

                // #5 notify (update) onlines list to all current customers
                foreach (KeyValuePair<long, Customer> entry in dictOnlineCustomers)
                {
                    // load onlines in caller
                    string lastMessage = "";
                    Clients.Caller.online(entry.Key, entry.Value.Name, lastMessage);
                    //Clients.All.online(entry.Key, entry.Value.Name);
                }

                // notify other this customer have enter.
                Clients.Others.enters(user.Id, user.Name);
            }

            return base.OnConnected();
        }

        // Can test lai ve ham disconnect nay
        //public override Task OnDisconnected(bool stopCalled)
        //{
        //    //var name = dic.FirstOrDefault(x => x.Value == Context.ConnectionId.ToString());
        //    var item = dictOnlineCustomers.FirstOrDefault(x => x.Value.Connections.Any(n => n.ConnectionId == Context.ConnectionId));
        //    Customer s;
        //    if (!dictOnlineCustomers.TryRemove(item.Key, out s))
        //        throw new Exception("Customer " + item.Key + " remove failed!");

        //    // notify caller about the disconnection if current chat view is still existing.
        //    Clients.Caller.selfDisconnected(item.Key, item.Value.Name);
        //    // notify all about the disconnection
        //    return Clients.All.disconnected(item.Key, item.Value.Name);
        //    //base.OnDisconnected(stopCalled: true);
        //}



        /// <summary>
        /// Kiem tra lai truong hop nao se goi
        /// </summary>
        /// <param name="stopCalled"></param>
        /// <returns></returns>
        public override Task OnDisconnected(bool stopCalled)
        {
            // #1 truon ghop refresh lai page
            // #2 truon ghop close tab or browser

            //var name = dic.FirstOrDefault(x => x.Value == Context.ConnectionId.ToString());
            var dictItem = dictOnlineCustomers.FirstOrDefault(x => x.Value.Connections.Any(n => n.ConnectionId == Context.ConnectionId));

            using (var db = new ChatSignalrContext())
            {
                // update connected status

                var customer = db.Customers
                    .FirstOrDefault(x => x.Id == dictItem.Key);
                var conn = customer.Connections.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
                conn.Connected = false;
                db.Entry(conn).State = EntityState.Modified;
                db.SaveChanges();

                var connItem = dictItem.Value.Connections.FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);
                dictItem.Value.Connections.Remove(connItem);
            }

            // if this customer not has nay connections: notify to all about this disconnection
            if (dictItem.Value.Connections.Count == 1)
            {
                Customer s;
                if (!dictOnlineCustomers.TryRemove(dictItem.Key, out s))
                    throw new Exception("Customer " + dictItem.Key + " remove failed!");

                // notify caller about the disconnection if current chat view is still existing.
                Clients.Caller.selfDisconnected(dictItem.Key, dictItem.Value.Name);
                // notify all about the disconnection
                Clients.All.disconnected(dictItem.Key, dictItem.Value.Name);
            }

            return base.OnDisconnected(stopCalled);
        }
    }
}