
function(PopulateOsInformaton)
    find_program( lsb_executable lsb_release )

    if( lsb_executable )
            execute_process( COMMAND ${lsb_executable} -is OUTPUT_VARIABLE DISTRO_ID OUTPUT_STRIP_TRAILING_WHITESPACE )
            execute_process( COMMAND ${lsb_executable} -rs OUTPUT_VARIABLE DISTRO_RELEASE OUTPUT_STRIP_TRAILING_WHITESPACE )
    else()
            if( EXISTS "/etc/os-release" )
                  file( STRINGS "/etc/os-release" DISTRO_ID REGEX "^ID=" )
                  file( STRINGS "/etc/os-release" DISTRO_RELEASE REGEX "^VERSION_ID=" )
                  string( REPLACE "ID=" "" DISTRO_ID "${DISTRO_ID}" )
                  string( REPLACE "VERSION_ID=" "" DISTRO_RELEASE "${DISTRO_RELEASE}" )
            endif( )
    endif()

    set (DD_OS_DISTRO_ID ${DISTRO_ID} PARENT_SCOPE)
    set (DD_OS_DISTRO_RELEASE ${DISTRO_RELEASE} PARENT_SCOPE)
endfunction()