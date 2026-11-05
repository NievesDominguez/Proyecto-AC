using System;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public ItemData item;
    public int amount;

    public bool IsEmpty => item == null || amount <= 0;

    public void Set(ItemData newItem, int newAmount)
    {
        item = newItem;
        amount = newAmount;
    }

    public void Clear()
    {
        item = null;
        amount = 0;
    }
}

[Serializable]
public struct StartingItem
{
    public ItemData item;
    [Min(1)] public int amount;
}

/// <summary>Resultado de intentar mover un objeto de una casilla a otra.</summary>
public enum MoveResult
{
    None,       // No pasó nada (por ejemplo, la pila de destino ya estaba completa)
    Moved,      // Se movió a una casilla vacía
    Merged,     // Se apiló sobre otra pila del mismo objeto
    Swapped     // Se intercambió con un objeto distinto
}

/// <summary>
/// Almacén de objetos del jugador: un número fijo de casillas, con apilado.
/// No sabe nada de la interfaz: solo guarda datos y avisa con eventos.
/// </summary>
public class Inventory : MonoBehaviour
{
    public static Inventory Instance { get; private set; }

    [SerializeField, Min(1)] private int capacity = 20;

    [Tooltip("Objetos con los que empieza la partida (útil para probar la interfaz).")]
    [SerializeField] private StartingItem[] startingItems;

    private InventorySlot[] slots;

    /// <summary>Se dispara cada vez que cambia el contenido.</summary>
    public event Action Changed;

    /// <summary>Se dispara al añadir objetos (objeto, cantidad realmente añadida).</summary>
    public event Action<ItemData, int> ItemAdded;

    /// <summary>Se dispara cuando no cabe todo lo que se intentó añadir.</summary>
    public event Action<ItemData> InventoryFull;

    public int Capacity => capacity;

    public int UsedSlots
    {
        get
        {
            EnsureSlots();

            int used = 0;

            foreach (InventorySlot slot in slots)
            {
                if (!slot.IsEmpty)
                {
                    used++;
                }
            }

            return used;
        }
    }

    private void Awake()
    {
        Instance = this;
        EnsureSlots();
    }

    private void Start()
    {
        if (startingItems == null)
        {
            return;
        }

        foreach (StartingItem entry in startingItems)
        {
            if (entry.item != null)
            {
                Add(entry.item, Mathf.Max(1, entry.amount), true);
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void EnsureSlots()
    {
        if (slots != null && slots.Length == capacity)
        {
            return;
        }

        slots = new InventorySlot[capacity];

        for (int i = 0; i < capacity; i++)
        {
            slots[i] = new InventorySlot();
        }
    }

    /// <summary>Devuelve la casilla (nunca null si el índice es válido).</summary>
    public InventorySlot GetSlot(int index)
    {
        EnsureSlots();

        if (index < 0 || index >= slots.Length)
        {
            return null;
        }

        return slots[index];
    }

    /// <summary>
    /// Intenta añadir objetos. Primero completa las pilas que ya existen
    /// y después usa casillas vacías. Devuelve cuántas unidades se añadieron.
    /// </summary>
    public int Add(ItemData item, int amount, bool silent = false)
    {
        if (item == null || amount <= 0)
        {
            return 0;
        }

        EnsureSlots();

        int remaining = amount;

        // 1. Completar pilas existentes
        foreach (InventorySlot slot in slots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (slot.IsEmpty || slot.item != item || slot.amount >= item.maxStack)
            {
                continue;
            }

            int toAdd = Mathf.Min(item.maxStack - slot.amount, remaining);
            slot.amount += toAdd;
            remaining -= toAdd;
        }

        // 2. Usar casillas vacías
        foreach (InventorySlot slot in slots)
        {
            if (remaining <= 0)
            {
                break;
            }

            if (!slot.IsEmpty)
            {
                continue;
            }

            int toAdd = Mathf.Min(item.maxStack, remaining);
            slot.Set(item, toAdd);
            remaining -= toAdd;
        }

        int added = amount - remaining;

        if (added > 0)
        {
            Changed?.Invoke();

            if (!silent)
            {
                ItemAdded?.Invoke(item, added);
            }
        }

        if (remaining > 0 && !silent)
        {
            InventoryFull?.Invoke(item);
        }

        return added;
    }

    // ------------------------------------------------------------------
    // OPERACIONES PARA MOVER OBJETOS CON LA "MANO"
    // ------------------------------------------------------------------

    /// <summary>
    /// Saca hasta 'amount' unidades de una casilla y las devuelve en 'item'.
    /// Devuelve cuántas se sacaron (0 si la casilla estaba vacía).
    /// </summary>
    public int Take(int index, int amount, out ItemData item)
    {
        item = null;

        InventorySlot slot = GetSlot(index);

        if (slot == null || slot.IsEmpty || amount <= 0)
        {
            return 0;
        }

        int taken = Mathf.Min(amount, slot.amount);
        item = slot.item;

        slot.amount -= taken;

        if (slot.amount <= 0)
        {
            slot.Clear();
        }

        Changed?.Invoke();
        return taken;
    }

    /// <summary>
    /// Coloca hasta 'amount' unidades en una casilla que esté vacía o que tenga
    /// el mismo objeto con hueco. Devuelve cuántas se colocaron (0 si no cabe nada).
    /// </summary>
    public int PlaceInto(int index, ItemData item, int amount)
    {
        InventorySlot slot = GetSlot(index);

        if (slot == null || item == null || amount <= 0)
        {
            return 0;
        }

        int placed;

        if (slot.IsEmpty)
        {
            placed = Mathf.Min(amount, item.maxStack);
            slot.Set(item, placed);
        }
        else if (slot.item == item)
        {
            placed = Mathf.Min(item.maxStack - slot.amount, amount);

            if (placed <= 0)
            {
                return 0;
            }

            slot.amount += placed;
        }
        else
        {
            return 0;
        }

        Changed?.Invoke();
        return placed;
    }

    /// <summary>
    /// Cambia lo que hay en una casilla ocupada por la pila indicada
    /// y devuelve lo que había antes.
    /// </summary>
    public bool SwapWith(int index, ItemData item, int amount, out ItemData oldItem, out int oldAmount)
    {
        oldItem = null;
        oldAmount = 0;

        InventorySlot slot = GetSlot(index);

        if (slot == null || item == null || amount <= 0 || slot.IsEmpty)
        {
            return false;
        }

        oldItem = slot.item;
        oldAmount = slot.amount;

        slot.Set(item, Mathf.Min(amount, item.maxStack));

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Mueve el contenido de una casilla a otra:
    ///  - si el destino está vacío, lo traslada;
    ///  - si tiene el mismo objeto, apila lo que quepa (el resto se queda en el origen);
    ///  - si tiene otro objeto, los intercambia.
    /// </summary>
    public MoveResult Move(int fromIndex, int toIndex)
    {
        InventorySlot from = GetSlot(fromIndex);
        InventorySlot to = GetSlot(toIndex);

        if (from == null || to == null || fromIndex == toIndex || from.IsEmpty)
        {
            return MoveResult.None;
        }

        // Destino vacío: traslado
        if (to.IsEmpty)
        {
            to.Set(from.item, from.amount);
            from.Clear();
            Changed?.Invoke();
            return MoveResult.Moved;
        }

        // Mismo objeto: apilado
        if (from.item == to.item)
        {
            int space = to.item.maxStack - to.amount;

            if (space <= 0)
            {
                return MoveResult.None;
            }

            int transfer = Mathf.Min(space, from.amount);
            to.amount += transfer;
            from.amount -= transfer;

            if (from.amount <= 0)
            {
                from.Clear();
            }

            Changed?.Invoke();
            return MoveResult.Merged;
        }

        // Objetos distintos: intercambio
        ItemData tempItem = from.item;
        int tempAmount = from.amount;

        from.Set(to.item, to.amount);
        to.Set(tempItem, tempAmount);

        Changed?.Invoke();
        return MoveResult.Swapped;
    }

    /// <summary>Quita unidades de una casilla. Devuelve false si no había suficientes.</summary>
    public bool RemoveAt(int index, int amount)
    {
        InventorySlot slot = GetSlot(index);

        if (slot == null || slot.IsEmpty || amount <= 0 || slot.amount < amount)
        {
            return false;
        }

        slot.amount -= amount;

        if (slot.amount <= 0)
        {
            slot.Clear();
        }

        Changed?.Invoke();
        return true;
    }

    /// <summary>
    /// Quita unidades de un objeto, estén en la casilla que estén. Si se indica una casilla
    /// preferida, se gasta primero de ahí. Devuelve false (sin quitar nada) si no hay suficientes.
    /// </summary>
    public bool RemoveItem(ItemData item, int amount, int preferredSlot = -1)
    {
        if (item == null || amount <= 0 || CountOf(item) < amount)
        {
            return false;
        }

        int remaining = amount;

        // 1. La casilla preferida (de donde el jugador eligió el objeto)
        InventorySlot preferred = GetSlot(preferredSlot);

        if (preferred != null && !preferred.IsEmpty && preferred.item == item)
        {
            int take = Mathf.Min(preferred.amount, remaining);
            preferred.amount -= take;
            remaining -= take;

            if (preferred.amount <= 0)
            {
                preferred.Clear();
            }
        }

        // 2. El resto, empezando por el final para respetar las pilas del principio
        for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            InventorySlot slot = slots[i];

            if (slot.IsEmpty || slot.item != item)
            {
                continue;
            }

            int take = Mathf.Min(slot.amount, remaining);
            slot.amount -= take;
            remaining -= take;

            if (slot.amount <= 0)
            {
                slot.Clear();
            }
        }

        Changed?.Invoke();
        return true;
    }

    public int CountOf(ItemData item)
    {
        EnsureSlots();

        int total = 0;

        foreach (InventorySlot slot in slots)
        {
            if (!slot.IsEmpty && slot.item == item)
            {
                total += slot.amount;
            }
        }

        return total;
    }
}