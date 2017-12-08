IF OBJECT_ID('dbo.petapoco','U') IS NOT NULL
	DROP TABLE dbo.petapoco;

CREATE TABLE petapoco (

	id				bigint IDENTITY(1,1) NOT NULL,
	title			varchar(127) NOT NULL,
	draft			bit NOT NULL,
	date_created	datetime NOT NULL,
	date_edited		datetime NULL,
	content			VARCHAR(MAX) NOT NULL,
	state			int NOT NULL,
	state2			int NULL,
	[col w space]	int,
	nullreal		real NULL,
	
	PRIMARY KEY (id)
);

IF OBJECT_ID('dbo.petapoco2','U') IS NOT NULL
	DROP TABLE dbo.petapoco2;

CREATE TABLE petapoco2 (
	email		varchar(127) NOT NULL,
	name		varchar(127) NOT NULL,
	PRIMARY KEY (email)
);

IF OBJECT_ID('dbo.composite_pk','U') IS NOT NULL
	DROP TABLE dbo.composite_pk;

CREATE TABLE composite_pk (
	id1		int NOT NULL,
	id2		int NOT NULL,
	value	varchar(100) NOT NULL,
	PRIMARY KEY (id1, id2)
);

IF OBJECT_ID('dbo.enum_string','U') IS NOT NULL
	DROP TABLE dbo.enum_string;

CREATE TABLE enum_string (
	id			int not null identity primary key,
	fruit_type	varchar(127) not null
);

IF OBJECT_ID('dbo.enum_integer','U') IS NOT NULL
	DROP TABLE dbo.enum_integer;

CREATE TABLE enum_integer (
	id			int not null identity primary key,
	fruit_type	int not null
);