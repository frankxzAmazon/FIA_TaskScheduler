CREATE TABLE [dbo].Job
(
	JobName varchar(50) NOT NULL,
	TriggerId uniqueidentifier not null,
	CONSTRAINT [PK_Job] PRIMARY KEY (JobName)
)
