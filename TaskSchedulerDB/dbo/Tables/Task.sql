CREATE TABLE [dbo].Task
(
	JobName varchar(50) NOT NULL,
	TaskId uniqueidentifier not null,
	Priority tinyint not null,
	ToWaitForFinish bit not null,
    CONSTRAINT PK_Task PRIMARY KEY (JobName, Priority), 
    CONSTRAINT [FK_Task_ToJob] FOREIGN KEY (JobName) REFERENCES dbo.Job(JobName)
)
