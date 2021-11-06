# dpostproc
 Decompilation post-processing

## VTBL Generator
This tool generates C++ source code from a supplied virtual table in the console.

### Example
Input:

```
__vt__Q24Game17EnemyAnimKeyEvent:
.4byte 0
.4byte 0
.4byte __dt__Q24Game17EnemyAnimKeyEventFv
.4byte getChildCount__5CNodeFv```

Output:

```
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
} // namespace Game```

The CNode class is not within the Game namespace, and as such this inconsistent scoping must be accounted for when implementing the tool's output.
