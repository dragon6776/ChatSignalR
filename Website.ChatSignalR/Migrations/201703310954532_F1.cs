namespace Website.ChatSignalR.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class F1 : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Connections", "ConnectionId", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Connections", "ConnectionId");
        }
    }
}
