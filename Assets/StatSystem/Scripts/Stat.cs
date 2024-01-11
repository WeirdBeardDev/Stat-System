using System;
using System.Collections.Generic;
using UnityEngine;

namespace StatSystem
{
[Serializable]
public sealed class Stat
{
   private const int DEFAULT_LIST_CAPACITY = 4;
   private const int DEFAULT_DIGIT_ACCURACY = 2;
   internal const int MAXIMUM_ROUND_DIGITS = 8;

   [SerializeField] private float baseValue;
   private readonly int _digitAccuracy;
   private readonly List<Modifier> _modifiersList = new();

   private readonly SortedList<ModifierType, IModifiersOperations> _modifiersOperations = new();
   private float _currentValue;
   private bool _isDirty;

   public Stat(float baseValue, int digitAccuracy, int modsMaxCapacity)
   {
      this.baseValue = baseValue;
      _currentValue = baseValue;
      _digitAccuracy = digitAccuracy;

      InitializeModifierOperations(modsMaxCapacity);
   }

   public Stat(float baseValue) : this(baseValue, DEFAULT_DIGIT_ACCURACY, DEFAULT_LIST_CAPACITY) { }
   public Stat(float baseValue, int digitAccuracy) : this(baseValue, digitAccuracy, DEFAULT_LIST_CAPACITY) { }

   public float BaseValue 
   {
      get => baseValue;
      set
      {
         baseValue = value;
         _currentValue = CalculateModifiedValue(_digitAccuracy);
         OnValueChanged();
      }
   }

   public float Value
   {
      get
      {
         if(IsDirty)
         {
            _currentValue = CalculateModifiedValue(_digitAccuracy);
            OnValueChanged();
         }
         return _currentValue;
      }
   }

   private bool IsDirty
   {
      get => _isDirty;
      set
      {
         _isDirty = value;
         if(_isDirty)
            OnModifiersChanged();
      }
   }

   public event Action ValueChanged;
   public event Action ModifiersChanged;

   public void AddModifier(Modifier modifier)
   {
      IsDirty = true;
      _modifiersOperations[modifier.Type].AddModifier(modifier);
   }

   public IReadOnlyList<Modifier> GetModifiers()
   {
      _modifiersList.Clear();
         
      foreach (var modifiersOperation in _modifiersOperations.Values)
         _modifiersList.AddRange(modifiersOperation.GetAllModifiers());

      return _modifiersList.AsReadOnly();
   }
   
   public IReadOnlyList<Modifier> GetModifiers(ModifierType modifierType) 
      => _modifiersOperations[modifierType].GetAllModifiers().AsReadOnly();

   public bool TryRemoveModifier(Modifier modifier)
   {
      var isModifierRemoved = false;

      if (_modifiersOperations[modifier.Type].TryRemoveModifier(modifier))
      {
         IsDirty = true;
         isModifierRemoved = true;
      }

      return isModifierRemoved;
   }

   public bool TryRemoveAllModifiersOf(object source)
   {
      bool isModifierRemoved = false;

      for (int i = 0; i < _modifiersOperations.Count; i++)
         isModifierRemoved = TryRemoveAllModifiersOfSourceFromList(source, 
                             _modifiersOperations.Values[i].GetAllModifiers()) || isModifierRemoved;

      return isModifierRemoved;
   }
   
   private float CalculateModifiedValue(int digitAccuracy)
   {
      digitAccuracy = Math.Clamp(digitAccuracy, 0, MAXIMUM_ROUND_DIGITS);

      float finalValue = baseValue;

      for (int i = 0; i < _modifiersOperations.Count; i++)
         finalValue += _modifiersOperations.Values[i].CalculateModifiersValue(baseValue, finalValue);

      IsDirty = false;

      return (float)Math.Round(finalValue, digitAccuracy);
   }

   private void InitializeModifierOperations(int capacity)
   {
      var modifierOperations = ModifierOperationsCollection.GetModifierOperations(capacity);
      
      foreach (var operationType in modifierOperations.Keys)
         _modifiersOperations[operationType] = modifierOperations[operationType]();
   }

   private bool TryRemoveAllModifiersOfSourceFromList(object source, List<Modifier> listOfModifiers)
   {
      bool isModifierRemoved = false;

      for (var i = listOfModifiers.Count - 1; i >= 0; i--)
      {
         if (ReferenceEquals(source, listOfModifiers[i].Source))
         {
            listOfModifiers.RemoveAt(i);
            IsDirty = true;
            isModifierRemoved = true;
         }
      }
      
      return isModifierRemoved;
   }

   private void OnValueChanged() => ValueChanged?.Invoke();
   private void OnModifiersChanged() => ModifiersChanged?.Invoke();
}
}
