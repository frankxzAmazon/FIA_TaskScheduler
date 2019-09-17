CREATE TABLE [dbo].EmailTrigger
(
	TriggerID uniqueidentifier default NEWSEQUENTIALID() PRIMARY KEY,
	EmailAddress varchar(100) null,
	MonitorFolder varchar(100) null,
	SubjectSubstring varchar(50) null,
	SubjectIsExactMatch bit default(0) NOT NULL,
	FileNameSubstring varchar(100) null,
	FileExtension varchar(10) null,
	FileNameIsExactMatch bit default(0) not null,
	MoveFolder varchar(100) null
)
