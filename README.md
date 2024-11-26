# Доклад по теме "Коллекция BigList<T>"

## Введение
Wintellect's Power Collections for .NET – библиотека, цель которой – реализация шаблонных классов коллекций, которые не были доступны в .NET Framework. Это довольно старая библиотека, которая обновляется не очень активно (последнее изменение в репозитории на github.com было сделано весной 2021 года). BigList<T> - одна из коллекций данной библиотеки. Данный класс похож на коллекцию List<T> из стандартного пространства имен System.Collections.Generic, однако он более оптимизирован для работы с большими списками (более 100 элементов), особенно для вставки, удаления, копии и конкатенации. Вставки и удаления в середине списка выполняются быстрее, чем в List<T>, за счет особой внутренней структуры.

## Публичные методы для работы с коллекцией
Ниже представлены публичные методы для работы с коллекцией:
- **BigList()** - создает пустую коллекцию с главным узлом `root` = `null`, ассимптотическая сложность - `O(N)`;
- **BigList(IEnumerable<T> collection)** - создает `BigList<T>` на соснове коллекции `IEnumerable<T>`;
- 

## Внутреннее устройство
В основе структуры данной коллекции лежит AVL-дерево. Данное дерево состоит из узлов. Данные узлы-объекты классов, производных от абстрактного класса `Node`, а именно `LeafNode` и `ConcatNode`. `LeafNode` - "лист", содержит в себе элементы коллекции типа `T` (в массиве `T[MAXLEAF]`, где `MAXLEAF` - максимальное кол-во элементов), `ConcatNode` отвечает за связь `LeafNode`. Узлы бывают разделенными, то есть могут использоваться в нескольких коллекциях, для этого необходимо, чтобы поле `shared` имело значение `true`, это повышает производительность, поскольку данные не копируются.

