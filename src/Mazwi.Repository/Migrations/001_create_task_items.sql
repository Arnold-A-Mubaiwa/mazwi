CREATE TABLE task_items (
    id uuid PRIMARY KEY,
    title varchar(200) NOT NULL,
    CONSTRAINT ck_task_items_title CHECK (char_length(btrim(title)) BETWEEN 1 AND 200)
);
