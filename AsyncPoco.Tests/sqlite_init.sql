DROP TABLE IF EXISTS petapoco;
DROP TABLE IF EXISTS petapoco2;
DROP TABLE IF EXISTS composite_pk;

CREATE TABLE petapoco (

	id				INTEGER PRIMARY KEY AUTOINCREMENT,
	title			TEXT NOT NULL,
	draft			BOOLEAN NOT NULL,
	date_created	DATETIME NOT NULL,
	date_edited		DATETIME NULL,
	content			TEXT NOT NULL,
	state			INTEGER NOT NULL,
	state2			INTEGER NULL,
	[col w space]	INTEGER,
	nullreal		REAL NULL
);

CREATE TABLE petapoco2 (
	email			TEXT NOT NULL,
	name			TEXT NOT NULL,
	PRIMARY KEY		(email)
);

CREATE TABLE composite_pk (
	id1				INT NOT NULL,
	id2				INT NOT NULL,
	value			TEXT NOT NULL,
	PRIMARY KEY		(id1, id2)
);
