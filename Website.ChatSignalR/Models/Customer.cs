using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Website.ChatSignalR.Models
{
    public class Customer
    {
        public Customer()
        {
            Connections = new List<Connection>();
        }

        public long Id { get; set; }
        public string Name { get; set; }

        public IList<Connection> Connections { get; set; }

        /// <summary>
        /// Many - to - Many
        /// </summary>
        public IList<Conversation> Conversations { get; set; }
    }

    public class Connection
    {
        public int Id { get; set; }
        public string UserAgent { get; set; }
        public bool Connected { get; set; }

        public virtual Customer Customer { get; set; }
        public string ConnectionId { get; internal set; }
    }
}