﻿using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace Website.ChatSignalR.Models
{
    public class ChatSignalrContext : DbContext
    {
        public DbSet<Customer> Customers { get; set; }
        public DbSet<Conversation> Conversations { get; set; }

        public System.Data.Entity.DbSet<Website.ChatSignalR.Models.Message> Messages { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Conversation>()
                .HasMany(t => t.Messages)
                .WithRequired(x => x.Conversation);

            base.OnModelCreating(modelBuilder);
        }
    }
}