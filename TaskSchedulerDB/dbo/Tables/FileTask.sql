CREATE TABLE [dbo].[FileTask] (
    [TaskId]            UNIQUEIDENTIFIER DEFAULT (newsequentialid()) NOT NULL,
    [DestinationFolder] VARCHAR (1000)   NOT NULL,
    [ToIncludeDate]     BIT              DEFAULT ((0)) NOT NULL,
    [DateFormat]        VARCHAR (20)     NULL,
    [ToUnzip]           BIT              DEFAULT ((0)) NOT NULL,
    [ToDeleteOriginal]  BIT              DEFAULT ((0)) NOT NULL,
    PRIMARY KEY CLUSTERED ([TaskId] ASC)
);


