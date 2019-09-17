CREATE TABLE [dbo].[CurrentStatus] (
    [JobName]       VARCHAR (50) NOT NULL,
    [CurrentStatus] VARCHAR (50) NOT NULL,
    [LastRun]       DATETIME     NOT NULL,
    PRIMARY KEY CLUSTERED ([JobName] ASC),
    CONSTRAINT [CK_CurrentStatus_CurrentStatus] CHECK ([CurrentStatus]='Running' OR [CurrentStatus]='Online' OR [CurrentStatus]='Offline')
);






