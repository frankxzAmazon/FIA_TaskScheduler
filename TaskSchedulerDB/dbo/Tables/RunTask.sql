CREATE TABLE [dbo].[RunTask] (
    [TaskId]                  UNIQUEIDENTIFIER DEFAULT (newsequentialid()) NOT NULL,
    [ProgramRunType]          VARCHAR (20)     NOT NULL,
    [ProgramLocation]         VARCHAR (1000)   NOT NULL,
    [ProgramName]             VARCHAR (100)    NOT NULL,
    [ProgramCommaDelimParams] VARCHAR (1000)   NULL,
    PRIMARY KEY CLUSTERED ([TaskId] ASC),
    CONSTRAINT [CK_RunEvent_RunType] CHECK ([ProgramRunType]='VBA' OR [ProgramRunType]='Command Line' OR [ProgramRunType]='Stored Procedure')
);


