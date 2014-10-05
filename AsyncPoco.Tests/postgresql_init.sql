
DROP TABLE IF EXISTS petapoco;

CREATE TABLE petapoco (
	id				bigserial NOT NULL,
	title			varchar(127) NOT NULL,
	draft			boolean NOT NULL,
	date_created	timestamp NOT NULL,
	date_edited		timestamp NULL,
	content			text NOT NULL,
	state			int NOT NULL,
	"col w space"   int,
	nullreal		real NULL,

	PRIMARY KEY (id)
);

DROP TABLE IF EXISTS petapoco2;

CREATE TABLE petapoco2 (
	email		varchar(127) NOT NULL,
	name		varchar(127) NOT NULL,
	PRIMARY KEY (email)
);

DROP TABLE IF EXISTS composite_pk;

CREATE TABLE composite_pk (
	id1		int NOT NULL,
	id2		int NOT NULL,
	value	varchar(100) NOT NULL,
	PRIMARY KEY (id1, id2)
);
