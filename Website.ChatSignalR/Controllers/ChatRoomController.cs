using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using System.Web;
using System.Web.Mvc;
using Website.ChatSignalR.Models;

namespace Website.ChatSignalR.Controllers
{
    public class ChatRoomController : Controller
    {
        private bool CheckUserExisted(int customerId)
        {
            using (var db = new ChatSignalrContext())
            {
                return db.Customers.Any(n => n.Id == customerId);
            }
        }

        // GET: ChatRoom
        public ActionResult Index()
        {
            return View();
        }

        public ActionResult SignalRChat()
        {
            // # BƯỚC NÀY SẼ CHUẨN BỊ DỮ LIỆU HỢP LỆ CHO ChatHub.cs
            // # KIEM TRA COOKIE HỢP LỆ
            // # NEU KO HỢP LỆ THÌ TẠO CUSTOMER MỚI & LƯU VÀO COOKIE MỚI

            var cookieValue = GetBasicCookie(AppConstants.COOKIE_CHAT_CUSTOMERID);

            if (string.IsNullOrEmpty(cookieValue))
            {
                //if (!int.TryParse(Request.Cookies.Get(AppConstants.COOKIE_CHAT_CUSTOMERID)?.Value, out customerId)  // neu ko parse dc customerId
                //    || !CheckUserExisted(customerId))    // neu customer ko ton tai trong db

                using (var db = new ChatSignalrContext())
                {
                    // add new customer
                    var index = db.Customers.Count() + 1;
                    var newCustomer = new Customer();
                    newCustomer.Name = "Guest";
                    db.Customers.Add(newCustomer);
                    db.SaveChanges();
                    newCustomer.Name += " " + newCustomer.Id;
                    db.Entry(newCustomer).State = System.Data.Entity.EntityState.Modified;
                    db.SaveChanges();

                    // set cookie customerid
                    //var newCookie = new HttpCookie(AppConstants.COOKIE_CHAT_CUSTOMERID, newCustomer.Id.ToString());
                    //newCookie.Expires = DateTime.Now.AddMonths(1);
                    //Response.SetCookie(newCookie);
                    SetBasicCookie(AppConstants.COOKIE_CHAT_CUSTOMERID, newCustomer.Id.ToString());
                }
            }

            return View();
        }

        private string GetBasicCookie(string key)
        {
            //key = RemoveSpecialChars(HttpContext.Current.Request.Url.Host) + "_" + key;
            HttpCookie myCookie = System.Web.HttpContext.Current.Request.Cookies[key];
            if (myCookie != null)
                return myCookie.Value;

            return null;
        }

        private void SetBasicCookie(string key, string value)
        {
            var myCookie = System.Web.HttpContext.Current.Request.Cookies[AppConstants.COOKIE_CHAT_CUSTOMERID] ?? new HttpCookie(AppConstants.COOKIE_CHAT_CUSTOMERID);
            myCookie.Value = value;
            myCookie.Expires = DateTime.Now.AddMonths(1);
            System.Web.HttpContext.Current.Response.Cookies.Add(myCookie);
        }

        public ActionResult LoadConversationMessages(long customerId, long toCustomerId)
        {
            using (var db = new ChatSignalrContext())
            {
                var listIds = new long[] { customerId, toCustomerId };
                var conv = db.Conversations
                    .Include(i => i.Messages)
                    .FirstOrDefault(x => x.Customers.Count == 2 && x.Customers.Any(n => listIds.Contains(n.Id)));

                var messages = conv?.Messages ?? new List<Message>();

                var result = messages
                    .Select(s => new
                    {
                        id = s.Id,
                        message = s.Content,
                        customerId = s.Customer.Id,
                        name = s.Customer.Name,
                        dateCreated = s.DateCreated.ToShortDateString(),
                    });

                return Json(result, JsonRequestBehavior.AllowGet);

                //return PartialView(conv.Messages);
            }
        }
    }
}