CREATE TABLE [dbo].[SharepointTrigger]
(
	TriggerID uniqueidentifier default NEWSEQUENTIALID() PRIMARY KEY,
	SharepointSite varchar(1000) not null,
	SharepointFolder varchar(1000) not null,
	FileNameSubstring varchar(100) null,
	FileNameIsExactMatch bit default(0) not null,
	FileExtension varchar(10) null
)
