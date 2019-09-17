CREATE TABLE [dbo].[FolderTrigger]
(
	TriggerID uniqueidentifier default NEWSEQUENTIALID() PRIMARY KEY,
	FolderToMonitor varchar(1000) not null,
	FileNameSubstring varchar(100) null,
	FileNameIsExactMatch bit default(0),
	FileExtension varchar(10) null
)
