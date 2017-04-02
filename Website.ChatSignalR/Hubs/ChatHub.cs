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

        #region Utilities

        private bool CheckExisted(int customerId)
        {
            using (var db = new ChatSignalrContext())
            {
                return db.Customers.Any(n => n.Id == customerId);
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


        #endregion

        public void SendToSpecific(string message, long toUserId)
        {
            var currentUserId = GetCurrentContextUserId();

            using (var db = new ChatSignalrContext())
            {
                // current user id from cookie phai luon co trong db, buoc nay da duoc kiem tra o ChatRoomController
                if (currentUserId == 0 || !db.Customers.Any(n => n.Id == currentUserId))
                {
                    Clients.Caller.showErrorMessage("Could not find current user (" + toUserId + ") or CurrentUserId is empty, please reload page again to get new UserId");
                    return;
                }

                var toUser = db.Customers
                    .Include(i => i.Connections)
                    .FirstOrDefault(x => x.Id == toUserId);

                var activeConnIds = toUser.Connections
                    .Where(x => x.Connected)
                    .Select(s => s.ConnectionId)
                    .ToList();

                if (toUser == null)
                {
                    Clients.Caller.showErrorMessage("Could not find dest user (" + toUserId + "), but this user will be received this message when this user is online");
                }
                else
                {
                    if (activeConnIds.Count == 0)
                    {
                        // thong bao cho nguoi gui // current caller connection only.
                        Clients.Caller.showErrorMessage("The user is no longer connected.");
                    }
                    else
                    {
                        // broadcast messae to caller
                        var caller = db.Customers
                            .Include(i => i.Connections)
                            .FirstOrDefault(x => x.Id == currentUserId);
                        var activeCallerConnIds = caller.Connections.Where(x => x.Connected)
                            .Select(s=>s.ConnectionId)
                            .ToList();

                        foreach (var connId in activeCallerConnIds)
                            Clients.Client(connId).broadcastMessage(currentUserId, toUser.Name, message); // Clients.Caller

                        // Gửi tin nhắn cho tất các các kết nối hiện tại của user can gui // all user connections
                        foreach(var connId in activeConnIds)
                            Clients.Client(connId).broadcastMessage(currentUserId, toUser.Name, message);
                    }

                    // save message to conversation
                    SaveMessageToConversation(currentUserId, toUserId, message);
                }
            }
        }

        /// <summary>
        /// Get current userid via from cookie data
        /// </summary>
        /// <returns></returns>
        private long GetCurrentContextUserId()
        {
            Cookie s;
            int customerId = 0;

            if (Context.Request.Cookies.TryGetValue(AppConstants.COOKIE_CHAT_CUSTOMERID, out s))
                int.TryParse(s.Value, out customerId);

            return customerId;
        }

        public override Task OnConnected()
        {
            var currentUserId = GetCurrentContextUserId();

            using (var db = new ChatSignalrContext())
            {
                // #1 - Check Valid: current user id from cookie phai luon co trong db, buoc nay da duoc kiem tra o ChatRoomController
                if (currentUserId == 0 || !db.Customers.Any(n => n.Id == currentUserId))
                {
                    // Nếu ko tìm thấy user , nhưng ko chắc chắn có set đc cookie ở trong Hub này không 
                    // nên tạm thời xử lý trường hợp này ở ChatRoomController.
                    Clients.Caller.showErrorMessage("Could not find current user (" + currentUserId + ") or CurrentUserId is empty, please reload page again to get new UserId");
                    return base.OnConnected();
                }

                // #2 - Add new user connection
                var user = db.Customers
                    .Include(i => i.Connections)
                    .FirstOrDefault(x => x.Id == currentUserId);
                user.Connections.Add(new Connection
                {
                    Connected = true,
                    UserAgent = Context.Request.Headers["User-Agent"],
                    ConnectionId = Context.ConnectionId
                });
                db.Entry(user).State = EntityState.Modified;
                db.SaveChanges();

                // #3 show list all online users to the caller
                var onlineUsers = db.Customers
                    .Include(i => i.Connections)
                    .Where(x => x.Connections.Any(n => n.Connected));

                foreach (var item in onlineUsers) // (KeyValuePair<long, Customer> entry in dictOnlineCustomers)
                {
                    // load onlines in caller
                    Clients.Caller.online(item.Id, item.Name);
                }

                // #4 notify this user have entered to the others.
                Clients.Others.enters(user.Id, user.Name);
            }

            return base.OnConnected();
        }

        private void TestDataDemo()
        {
            using (var db = new ChatSignalrContext())
            {
                var allConn = db.Customers
                    .Include(i => i.Connections)
                    .SelectMany(s => s.Connections)
                    .Where(x => x.Connected)
                    .ToList();

                foreach (var item in allConn)
                {
                    item.Connected = false;
                    db.Entry(item).State = EntityState.Modified;
                    db.SaveChanges();
                }
            }
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            using (var db = new ChatSignalrContext())
            {
                var user = db.Customers
                    .Include(i => i.Connections)
                    .FirstOrDefault(x => x.Connections.Any(n => n.ConnectionId == Context.ConnectionId));

                var conn = user.Connections
                    .FirstOrDefault(x => x.ConnectionId == Context.ConnectionId);

                conn.Connected = false;
                db.Entry(conn).State = EntityState.Modified;
                db.SaveChanges();

                if (!user.Connections.Any(n => n.Connected))
                {
                    // If all user connections is disconnected, notify to all about this disconnection of the user.
                    Clients.All.disconnected(user.Id, user.Name);
                }
            }

            return base.OnDisconnected(stopCalled);
        }
    }
}