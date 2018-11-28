using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;

namespace ChangeTracking
{
    internal sealed class ChangeTrackingBindingList<T> : BindingList<T>, INotifyCollectionChanged where T : class
    {
        private readonly Action<T> _ItemCanceled;
        private Action<T> _DeleteItem;
        private readonly bool _MakeComplexPropertiesTrackable;
        private readonly bool _MakeCollectionPropertiesTrackable;
        private ListSortDirection _sortDirection;
        private PropertyDescriptor _sortProperty;
        private bool _isSorted;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public ChangeTrackingBindingList(IList<T> list, Action<T> deleteItem, Action<T> itemCanceled, bool makeComplexPropertiesTrackable, bool makeCollectionPropertiesTrackable)
            : base(list)
        {
            _DeleteItem = deleteItem;
            _ItemCanceled = itemCanceled;
            _MakeComplexPropertiesTrackable = makeComplexPropertiesTrackable;
            _MakeCollectionPropertiesTrackable = makeCollectionPropertiesTrackable;
            var bindingListType = typeof(ChangeTrackingBindingList<T>).BaseType;
            bindingListType.GetField("raiseItemChangedEvents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(this, true);
            var hookMethod = bindingListType.GetMethod("HookPropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            foreach (var item in list)
            {
                hookMethod.Invoke(this, new object[] { item });
            }
        }

        protected override void InsertItem(int index, T item)
        {
            object trackable = item as IChangeTrackable<T>;
            if (trackable == null)
            {
                trackable = item.AsTrackable(ChangeStatus.Added, _ItemCanceled, _MakeComplexPropertiesTrackable, _MakeCollectionPropertiesTrackable);
            }
            base.InsertItem(index, (T)trackable);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
        }

        protected override void SetItem(int index, T item)
        {
            object trackable = item as IChangeTrackable<T>;
            if (trackable == null)
            {
                trackable = item.AsTrackable(ChangeStatus.Added, _ItemCanceled, _MakeComplexPropertiesTrackable, _MakeCollectionPropertiesTrackable);
            }
            T originalItem = this[index];
            base.SetItem(index, (T)trackable);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, originalItem, index));
        }

        protected override void RemoveItem(int index)
        {
            T removedItem = this[index];
            _DeleteItem?.Invoke(removedItem);
            base.RemoveItem(index);
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
        }

        protected override void OnListChanged(ListChangedEventArgs e)
        {
            base.OnListChanged(e);
            switch (e.ListChangedType)
            {
                case ListChangedType.Reset:
                    CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
                    break;
                default:
                    return;
            }
        }

        protected override object AddNewCore()
        {
            AddingNewEventArgs e = new AddingNewEventArgs(null);
            OnAddingNew(e);
            T newItem = (T)e.NewObject;

            if (newItem == null)
            {
                newItem = Activator.CreateInstance<T>();
            }

            object trackable = newItem as IChangeTrackable<T>;
            if (trackable == null)
            {
                trackable = newItem.AsTrackable(ChangeStatus.Added, _ItemCanceled, _MakeComplexPropertiesTrackable, _MakeCollectionPropertiesTrackable);
                var editable = (IEditableObject)trackable;
                editable.BeginEdit();
            }
            Add((T)trackable);

            return trackable;
        }

        protected override void ApplySortCore(PropertyDescriptor prop,
            ListSortDirection direction)
        {
            _isSorted = true;
            _sortDirection = direction;
            _sortProperty = prop;

            Func<T, object> predicate = n => n.GetType().GetProperty(prop.Name)
                .GetValue(n, null);

            ResetItems(_sortDirection == ListSortDirection.Ascending
                ? Items.AsParallel().OrderBy(predicate)
                : Items.AsParallel().OrderByDescending(predicate));
        }

        protected override void RemoveSortCore()
        {
            _isSorted = false;
            _sortDirection = base.SortDirectionCore;
            _sortProperty = base.SortPropertyCore;

            ResetBindings();
        }

        private void ResetItems(IEnumerable<T> items)
        {
            RaiseListChangedEvents = false;
            var tempList = items.ToList();
            ClearItems();

            foreach (var item in tempList) Add(item);

            RaiseListChangedEvents = true;
            ResetBindings();
        }

        protected override bool SupportsSortingCore => true;

        protected override bool IsSortedCore => _isSorted;

        protected override ListSortDirection SortDirectionCore => _sortDirection;

        protected override PropertyDescriptor SortPropertyCore => _sortProperty;
    }
}
