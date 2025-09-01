"""
ASCII art for hoho terminal interface
"""

# The official Saitama "OK" face - based on banner art
OK_FACE_SAITAMA = """
                           @@@@@@@@@@@@@@@@@
                          %@@@@@@@@@@@@@@@@@@@@@
                    =@@@@@@@                @@@@@@
                   @@@@@@@@*                   +@@@
                 @@@@                             @@@
                #@@                                @@@
               @@@                                  =@@
               @*                                    @@%
                 -+:                                  @@
             @@@@##-          -               @@       @@
             @@              @@@@@@@@@@      #@@@@@@@# @@
             @@@@          @@ @       @@     #@      @ @@
             @  @@@        @  @ @@@@- =-  =+   @@@@  . @@
           +@@ .@@@        @@% @    = @:  @:*@ *  =  @ @@
           @     @           @@@.  +@@@   @ @ @@  @@@  @@
           @ @@.@@                        @ @          @@
           @@  @ @@@@                      @ @         @@
           @@@@@ :@ -#                     @ @         @@
            #  @@@@ +-                      :          @@
            .@+                         @@= @@        @@*
              #@@@@@@                   @ @* @        @@
                    @@                  @@%=@@       @@
                     @@.                            @@
                      @@@@@%                      @@@
                       @@@@@@                   @@@@
                           *@@@@:            @@@@@
                            *@@@@@@@@@@@@@@@@@@@
                                @@@@@@@@@@@@@
"""

def get_ok_face(size="saitama"):
    """Get the appropriate OK face ASCII art by size"""
    faces = {
        "saitama": OK_FACE_SAITAMA,  # The official one
        "large": OK_FACE_LARGE,
        "medium": OK_FACE_MEDIUM,
        "small": OK_FACE_SMALL,
        "mini": OK_FACE_MINI,
        "text": OK_FACE_TEXT,
        "dots": OK_FACE_DOTS
    }
    return faces.get(size, OK_FACE_SAITAMA)  # Default to the real Saitama