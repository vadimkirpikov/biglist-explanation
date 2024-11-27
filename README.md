# Доклад по теме "Коллекция BigList<T>"

## Введение
Wintellect's Power Collections for .NET – библиотека, цель которой – реализация шаблонных классов коллекций, которые не были доступны в .NET Framework. Это довольно старая библиотека, которая обновляется не очень активно (последнее изменение в репозитории на github.com было сделано весной 2021 года). BigList<T> - одна из коллекций данной библиотеки. Данный класс похож на коллекцию List<T> из стандартного пространства имен System.Collections.Generic, однако он более оптимизирован для работы с большими списками (более 100 элементов), особенно для вставки, удаления, копии и конкатенации. Вставки и удаления в середине списка выполняются быстрее, чем в List<T>, за счет особой внутренней структуры.

## Публичные методы для работы с коллекцией
Ниже представлены публичные методы для работы с коллекцией:
- **BigList()** - создает пустую коллекцию с главным узлом `root` = `null`, алгоритмическая сложность;
- **BigList(IEnumerable<T> collection)** - создает `BigList<T>` на соснове коллекции `IEnumerable<T>`, алгоритмическая сложность - `O(N)`, где `N` - кол-во элементов в коллекции;
- **BigList(IEnumerable<T> collection, int copies)** - создает `BigList<T>`, содержащий `copies` копий коллекции `IEnumerable<T>`, алгоритмичиская сложность  - `O(N + log(K)`, где `N` - кол-во элементов в коллекции, `K` - кол-во копий;
- **BigList(BigList<T> list)** - создает `BigList<T>` на основе другого `BigList<T>`, алгоритмическая сложность - константная в лучшем случае, поскольку предусмотрена система общего хранища, однако изменение коллекции требует больше времени из-за того, что элементы копируются;
- **BigList(BigList<T> list, int copies)**  - создает `BigList<T>`, содержащий `copies` копий другого `BigList<T>`, алгоритмическая сложность - `O(log(K))`, где `K` - кол-во копий коллекции (в лучшем случае, однако любое изменение коллекции требует дополнительного времени), и `O(log(K))` памяти;
- **T this[int index]** - обращение к элементу коллекции по индексу, алгоритмическая сложность - `O(log N)`;
- **void Clear()** - очищает всю коллекцию, алгоритмическая сложность - константная (`O(1)`);
- **void Insert(int index, T item)** - вставляет элемент `item` по индексу `index`, алгоритмическая сложность - `O(log(N))`, но вставка в начало и в конец займет `O(N)`;
- **void InsertRange(int index, IEnumerable<T> collection)** - всталвялет коллекцию `collection`, начиная с позиции `index`, алгоритмическая сложность - `O(M + log(N))`, где `M` - кол-во элементов вставляемой коллекции, а `N` - кол-во элементов в текущей коллекции;
- **void InsertRange(int index, BigList<T> list)** - вставляет другой `BigList<T> list` в коллекцию, начиная с позиции `index`, алгоритмическая сложность - `O(log(N))`(в лучшем случае), где `N` - кол-во элементов в текущей коллекции;
- **void RemoveAt(int index)** - удаляет из коллекции по индексу `index`, алгоритмическая сложность - `O(log(N))`, где `N`- кол-во элементов в текущей коллекции;
- **void RemoveRange(int index, int count)** - удаляет интервал из коллекции, начиная с позиции `index` заканчивая позицией `index+count`не в ключительно, алгоритмическая сложность - `O(count + Min(index, Count - 1 - index))`;
- **void Add(T item)** - добавляет элемент в конец коллекции за константное время;
- **void AddToFront(T item)** - добавляет элемент в начало коллекции;
- **void AddRange(IEnumerable<T> collection)** - добавляет коллекцию, реализующую интерфейс `IEnumerable<T>` в текущую коллекцию, алгоритмическая сложность - `O(M + log(N))`, где `M` - кол-во элементов во вставляемой коллекции, `N` - размер текущей коллекции;
- **void Clone()** - клонирует текущую коллекцию за константное вермя, с общим хранилищем;
- 


## Внутреннее устройство
В основе структуры данной коллекции лежит AVL-дерево. Данное дерево состоит из узлов. Данные узлы-объекты классов, производных от абстрактного класса `Node`, а именно `LeafNode` и `ConcatNode`. `LeafNode` - "лист", содержит в себе элементы коллекции типа `T` (в массиве `T[MAXLEAF]`, где `MAXLEAF` - максимальное кол-во элементов), `ConcatNode` отвечает за связь `LeafNode`. Узлы бывают разделенными, то есть могут использоваться в нескольких коллекциях, для этого необходимо, чтобы поле `shared` имело значение `true`, это повышает производительность, поскольку данные не копируются.

