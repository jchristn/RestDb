CREATE TABLE person (
    id INTEGER PRIMARY KEY,
    firstname VARCHAR(64) COLLATE NOCASE,
    lastname VARCHAR(64) COLLATE NOCASE,
    birthday DATETIME
);

INSERT INTO person (firstname, lastname, birthday) VALUES
    ('George', 'Washington', '1732-02-22'),
    ('Abraham', 'Lincoln', '1809-02-12'),
    ('Franklin', 'Roosevelt', '1882-01-30'),
    ('John', 'Kennedy', '1917-05-29'),
    ('Ronald', 'Reagan', '1911-02-06'),
    ('Barack', 'Obama', '1961-08-04'),
    ('Donald', 'Trump', '1946-06-14'),
    ('Joe', 'Biden', '1942-11-20');
