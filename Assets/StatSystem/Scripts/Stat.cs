using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;

namespace StatSystem
{
public sealed class Stat
{
   public float Value
   {
      get
      {
         if(_isDirty)
         {
            _currentValue = CalculateModifiedValue(_digitAccuracy);
         }

         return _currentValue;
      }
   }

   public float BaseValue {
      get => _baseValue;
      set
      {
         _baseValue = value;
         _currentValue = CalculateModifiedValue(_digitAccuracy);
      }
   }

   public (IReadOnlyList<Modifier> FlatModifiers, IReadOnlyList<Modifier> AdditiveModifiers, IReadOnlyList<Modifier>
      MultiplicativeModifiers) Modifiers =>
      (_flatModifiers.AsReadOnly(), _additivePercentageModifiers.AsReadOnly(), _multiplicativePercentageModifiers.AsReadOnly());

   // Gets raised only when the value is calculated with the available modifiers,
   // NOT whenever a modifier id added/removed.
   public event Action ValueChanged;
   
   private const int MAXIMUM_ROUND_DIGITS = 8;

   private readonly List<Modifier> _flatModifiers;
   private readonly List<Modifier> _additivePercentageModifiers;
   private readonly List<Modifier> _multiplicativePercentageModifiers;

   private float _baseValue;
   private float _currentValue;
   private bool _isDirty;
   private readonly int _digitAccuracy;
   
   public Stat(float baseValue, int digitAccuracy, int flatModsMaxCapacity, int additiveModsMaxCapacity, int multiplicativeModsMaxCapacity)
   {
      _baseValue = baseValue;
      _currentValue = baseValue;
      _digitAccuracy = digitAccuracy;
      
      _flatModifiers = new List<Modifier>(flatModsMaxCapacity);
      _additivePercentageModifiers = new List<Modifier>(additiveModsMaxCapacity);
      _multiplicativePercentageModifiers = new List<Modifier>(multiplicativeModsMaxCapacity);
   }
   public Stat(float baseValue) : this(baseValue, 4, 4, 4, 4) { }
   public Stat(float baseValue, int digitAccuracy) : this(baseValue, digitAccuracy, 4, 4, 4) { }

   public void AddModifier(Modifier modifier)
   {
      _isDirty = true;
      
      switch (modifier.Type)
      {
         case ModifierType.Flat:
            CheckListCapacity(_flatModifiers);
           _flatModifiers.Add(modifier);
            break;
         case ModifierType.Additive:
            CheckListCapacity(_additivePercentageModifiers);
            _additivePercentageModifiers.Add(modifier);
            break;
         case ModifierType.Multiplicative:
            CheckListCapacity(_multiplicativePercentageModifiers);
            _multiplicativePercentageModifiers.Add(modifier);
            break;
         default:
            throw new ArgumentOutOfRangeException();
      }
   }

   public bool TryRemoveModifier(Modifier modifier)
   {
      return modifier.Type switch
      {
         ModifierType.Flat => TryRemoveModifierFromList(modifier, _flatModifiers),
         ModifierType.Additive => TryRemoveModifierFromList(modifier, _additivePercentageModifiers),
         ModifierType.Multiplicative => TryRemoveModifierFromList(modifier,
            _multiplicativePercentageModifiers),
         _ => throw new ArgumentOutOfRangeException()
      };
   }

   public bool TryRemoveAllModifiersOf(object source) =>
      TryRemoveAllModifiersOfSourceFromList(source, _flatModifiers) ||
      TryRemoveAllModifiersOfSourceFromList(source, _additivePercentageModifiers) ||
      TryRemoveAllModifiersOfSourceFromList(source, _multiplicativePercentageModifiers);

   // For avoiding evaluation from the value getter
   public void ForceModifiersCalculation() => CalculateModifiedValue(_digitAccuracy);
   
   private float CalculateModifiedValue(int roundDigits)
   {
      roundDigits = Math.Clamp(roundDigits, 0, MAXIMUM_ROUND_DIGITS);
      
      var flatModsValue = CalculateFlatModsValue(_baseValue);
      var additiveModsValue = CalculateAdditiveModsValue(_baseValue);
      var finalValue = CalculateMultiplicativeModsValue(flatModsValue + additiveModsValue);

      _isDirty = false;
      OnValueChanged();
      
      return (float)Math.Round(finalValue, roundDigits);
   }

   private float CalculateFlatModsValue(float startingValue)
   {
      var calculatedValue = startingValue;
      float flatModifiersSum = 0f;
      
      for (var i = 0; i < _flatModifiers.Count; i++)
         flatModifiersSum += _flatModifiers[i];

      calculatedValue += flatModifiersSum;

      return calculatedValue;
   }

   private float CalculateAdditiveModsValue(float startingValue)
   {
      var calculatedValue = startingValue;
      float additiveModifiersSum = 0f;
      
      for (var i = 0; i < _additivePercentageModifiers.Count; i++)
         additiveModifiersSum += _additivePercentageModifiers[i];

      calculatedValue += _baseValue * (1 + additiveModifiersSum);
      
      return calculatedValue;
   }

   private float CalculateMultiplicativeModsValue(float startingValue)
   {
      var calculatedValue = startingValue;
      
      for (var i = 0; i < _multiplicativePercentageModifiers.Count; i++)
         calculatedValue *= 1 + _multiplicativePercentageModifiers[i];

      return calculatedValue;
   }

   private bool TryRemoveModifierFromList(Modifier modifier, List<Modifier> listOfModifiers)
   {
      if (listOfModifiers.Remove(modifier))
      {
         _isDirty = true;
         return true;
      }
      
      return false;
   }
   
   private bool TryRemoveAllModifiersOfSourceFromList(object source, List<Modifier> listOfModifiers)
   {
      var isModifierRemoved = false;
      
      for (var i = 0; i < listOfModifiers.Count; i++)
      {
         if (ReferenceEquals(source, listOfModifiers[i].Source))
         {
            listOfModifiers.RemoveAt(i);
            _isDirty = true;
            isModifierRemoved = true;
         }
      }
      
      return isModifierRemoved;
   }

   private void OnValueChanged() => ValueChanged?.Invoke();

   [Conditional("UNITY_EDITOR")]
   private static void CheckListCapacity(List<Modifier> modifiersList, [CallerArgumentExpression("modifiersList")] string name = null)
   {
#if UNITY_EDITOR
      if(modifiersList.Count == modifiersList.Capacity)
         Debug.LogWarning($"Resize of {name} List! Consider initializing the list with higher capacity.");
#endif
   }
}
}