using System;
using NOVR.VrUi;
using UnityEngine;

namespace NOVR;

public class EventBus : MonoBehaviour
{
    #region API Properties
    public static Camera MainCamera => _previousMainCamera;
    public static Camera CockpitHudCamera => NOUIManager.I.CockpitHudCamera;
    #endregion
    
    
    public static Action<Camera?, Camera?> OnMainCameraChanged;       
    private static Camera? _previousMainCamera; 
    
    

    
    private static EventBus? _current;


    private bool _loggedExtraEventsError = false;
    private HandoffState _state = HandoffState.NoState;
    

    private EventBus()
    {
        if (CheckExtraDispatchers())
        {
            string trace = StackTraceUtility.ExtractStackTrace();
            Debug.Log($"{typeof(EventBus)} ctor stack: " + trace);
        }
    }
    
    public void Update()
    {
        if (!CheckExtraDispatchers()) return;


        var newMainCamera = Camera.main;                                                         
        if (newMainCamera != _previousMainCamera)                                                
        {                                                                                        
            OnMainCameraChanged(_previousMainCamera, newMainCamera);                             
            _previousMainCamera = newMainCamera;                                                 
        }                                                                                        
        
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>Should continue executing</returns>
    /// <exception cref="Exception">If internal state is deemed impossible</exception>
    private bool CheckExtraDispatchers()
    {
        var oldState = _state;
        _state = GetDispatcherHandoffState(_state);
        switch (_state)
        {
            case HandoffState.NoState:
            case HandoffState.GodFuckingKnows:
                throw new Exception($"Invalid state in {typeof(EventBus)}! State: {_state}");
            case HandoffState.InitialDispatcher:
                _current = this;
                return true;
            case HandoffState.AwaitingPreviousDisposal:
                LogExtraDispatcher();
                return false;
            case HandoffState.ProperHandoff:
                _current = this;
                return true;
            case HandoffState.ImproperHandoff:
                _current = this;
                if (oldState == HandoffState.AwaitingPreviousDisposal)
                    Debug.LogWarning($"{typeof(EventBus)}: Successful handoff after suspicious state");
                return true;
        }
        throw new Exception("How did we get here?");
    }

    private HandoffState GetDispatcherHandoffState(HandoffState current)
    {
        bool isNull = _current is null;                             
        

        if (isNull) return HandoffState.InitialDispatcher;
        bool unityDisposed = _current != null;
        bool isUs = _current == this;
        if (isUs) return current;   
        if (unityDisposed)
        {
            if (current == HandoffState.NoState) return HandoffState.ProperHandoff;
            if (current == HandoffState.AwaitingPreviousDisposal) return HandoffState.ImproperHandoff;
        }

        return HandoffState.GodFuckingKnows;
    }

    private void LogExtraDispatcher()
    {
        if (!_loggedExtraEventsError) return;
        Debug.LogError($"Additional instances of {typeof(EventBus)}. This should not happen!");
        
        _loggedExtraEventsError = true;
    }



    private enum HandoffState
    {
        NoState,
        InitialDispatcher,          
        AwaitingPreviousDisposal,   
        ProperHandoff,                            
        ImproperHandoff,                   
        GodFuckingKnows = -1
    }
 
 
}