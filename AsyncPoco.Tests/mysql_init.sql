DROP TABLE IF EXISTS petapoco;

CREATE TABLE petapoco (

	id				serial,
	title			varchar(127) NOT NULL,
	draft			BOOL NOT NULL,
	date_created	datetime NOT NULL,
	date_edited		datetime NULL,
	content			longtext NOT NULL,
	state			smallint UNSIGNED NOT NULL,
	`col w space`	int,
	nullreal		float NULL,
	
	PRIMARY KEY (id)
) ENGINE=INNODB;

DROP TABLE IF EXISTS petapoco2;

CREATE TABLE petapoco2 (
	email		varchar(127) NOT NULL,
	name		varchar(127) NOT NULL,
	PRIMARY KEY (email)
) ENGINE=INNODB;

DROP TABLE IF EXISTS composite_pk;

CREATE TABLE composite_pk (
	id1		int NOT NULL,
	id2		int NOT NULL,
	value	varchar(100) NOT NULL,
	PRIMARY KEY (id1, id2)
) ENGINE=INNODB;
