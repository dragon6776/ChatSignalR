using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Website.ChatSignalR.Models
{
    public class Conversation
    {
        public Conversation()
        {
            Messages = new List<Message>();
            Customers = new List<Customer>();
        }

        public int Id { get; set; }
        /// <summary>
        /// one - many
        /// </summary>
        public IList<Message> Messages { get; set; }

        /// <summary>
        /// many - many
        /// </summary>
        public IList<Customer> Customers { get; set; }

        /// <summary>
        /// for accessing first message
        /// </summary>
        public virtual Message FirstMessage { get; set; }

        /// <summary>
        /// for accessing lastest message
        /// </summary>
        public virtual Message LastMessage { get; set; }
    }

    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime DateCreated { get; set; }
        public virtual Customer Customer { get; set; }
    }
}