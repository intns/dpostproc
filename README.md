# dpostproc
 Decompilation post-processing

## VTBL Generator
This tool generates C++ source code from a supplied virtual table in the console.

### Example
Input:

```asm
__vt__Q24Game17EnemyAnimKeyEvent:
.4byte 0
.4byte 0
.4byte __dt__Q24Game17EnemyAnimKeyEventFv
.4byte getChildCount__5CNodeFv
```

Output:

```cpp
// NOTE: THE SCOPE AND FUNCTION OFFSETS OF ALL CLASSES BESIDES THE ONE YOU INPUTTED MAY BE WRONG!

namespace Game { // Unsure if namespace or structure!
struct CNode
{
        virtual void getChildCount(); // _00

        // _00, VTBL
};

struct EnemyAnimKeyEvent : public CNode
{
        ~EnemyAnimKeyEvent()          // _00, from Game::
        virtual void getChildCount(); // _04, from CNode::

        // _00, VTBL
};
} // namespace Game
```

The CNode class is not within the Game namespace, and as such this inconsistent scoping must be accounted for when implementing the tool's output.

## Shifter
This tool recognises and fixes hardcoded addresses in .s files, this "fix" is a simple annotation using a label and then referencing the label where the address is found.

You will have to do extra searches for false positives and .data to .data pointers, although this solution can decrease the amount of pointers from tens of thousands to hundreds, making it much quicker and easier for shiftability to occur (hence the name Shifter).
