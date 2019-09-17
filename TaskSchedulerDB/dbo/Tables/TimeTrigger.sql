CREATE TABLE [dbo].[TimeTrigger] (
    [TriggerID]               UNIQUEIDENTIFIER DEFAULT (newsequentialid()) NOT NULL,
    [FirstExecutionTime(EST)] DATETIME         NOT NULL,
    [ExecutionFrequency]      SMALLINT         NOT NULL,
    [ExecutionFrequencyUnits] VARCHAR (20)     NOT NULL,
    PRIMARY KEY CLUSTERED ([TriggerID] ASC),
    CONSTRAINT [CK_TimeTrigger_Frequency] CHECK ([ExecutionFrequencyUnits]='Years' OR [ExecutionFrequencyUnits]='Months' OR [ExecutionFrequencyUnits]='Weeks' OR [ExecutionFrequencyUnits]='Days' OR [ExecutionFrequencyUnits]='Hours' OR [ExecutionFrequencyUnits]='Minutes')
);


