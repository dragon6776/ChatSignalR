namespace Website.ChatSignalR.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class F2 : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Conversations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        FirstMessage_Id = c.Int(),
                        LastMessage_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Messages", t => t.FirstMessage_Id)
                .ForeignKey("dbo.Messages", t => t.LastMessage_Id)
                .Index(t => t.FirstMessage_Id)
                .Index(t => t.LastMessage_Id);
            
            CreateTable(
                "dbo.Messages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Content = c.String(),
                        DateCreated = c.DateTime(nullable: false),
                        Customer_Id = c.Long(),
                        Conversation_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Customers", t => t.Customer_Id)
                .ForeignKey("dbo.Conversations", t => t.Conversation_Id)
                .Index(t => t.Customer_Id)
                .Index(t => t.Conversation_Id);
            
            CreateTable(
                "dbo.CustomerConversations",
                c => new
                    {
                        Customer_Id = c.Long(nullable: false),
                        Conversation_Id = c.Int(nullable: false),
                    })
                .PrimaryKey(t => new { t.Customer_Id, t.Conversation_Id })
                .ForeignKey("dbo.Customers", t => t.Customer_Id, cascadeDelete: true)
                .ForeignKey("dbo.Conversations", t => t.Conversation_Id, cascadeDelete: true)
                .Index(t => t.Customer_Id)
                .Index(t => t.Conversation_Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Messages", "Conversation_Id", "dbo.Conversations");
            DropForeignKey("dbo.Conversations", "LastMessage_Id", "dbo.Messages");
            DropForeignKey("dbo.Conversations", "FirstMessage_Id", "dbo.Messages");
            DropForeignKey("dbo.Messages", "Customer_Id", "dbo.Customers");
            DropForeignKey("dbo.CustomerConversations", "Conversation_Id", "dbo.Conversations");
            DropForeignKey("dbo.CustomerConversations", "Customer_Id", "dbo.Customers");
            DropIndex("dbo.CustomerConversations", new[] { "Conversation_Id" });
            DropIndex("dbo.CustomerConversations", new[] { "Customer_Id" });
            DropIndex("dbo.Messages", new[] { "Conversation_Id" });
            DropIndex("dbo.Messages", new[] { "Customer_Id" });
            DropIndex("dbo.Conversations", new[] { "LastMessage_Id" });
            DropIndex("dbo.Conversations", new[] { "FirstMessage_Id" });
            DropTable("dbo.CustomerConversations");
            DropTable("dbo.Messages");
            DropTable("dbo.Conversations");
        }
    }
}
