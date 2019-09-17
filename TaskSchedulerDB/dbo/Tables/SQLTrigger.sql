CREATE TABLE [dbo].[SQLTrigger] (
    [TriggerID]    UNIQUEIDENTIFIER DEFAULT (newsequentialid()) NOT NULL,
    [InstanceName] VARCHAR (128)    NOT NULL,
    [DBName]       VARCHAR (128)    NOT NULL,
    [SQLCode]      VARCHAR (MAX)    NOT NULL,
    [EventType]    VARCHAR (50)     NOT NULL,
    PRIMARY KEY CLUSTERED ([TriggerID] ASC),
    CONSTRAINT [CK_SQLTrigger_EventType] CHECK ([EventType]='Update' OR [EventType]='Unknown' OR [EventType]='Truncate' OR [EventType]='TemplateLimit' OR [EventType]='Restart' OR [EventType]='Resource' OR [EventType]='Query' OR [EventType]='PreviousFire' OR [EventType]='Options' OR [EventType]='Merge' OR [EventType]='Isolation' OR [EventType]='Invalid' OR [EventType]='Insert' OR [EventType]='Expired' OR [EventType]='Error' OR [EventType]='Drop' OR [EventType]='Delete' OR [EventType]='Alter' OR [EventType]='AlreadyChanged')
);


