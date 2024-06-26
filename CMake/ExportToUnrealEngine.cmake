
function(_ue4_get_target_link_libraries addTarget target_file_list target_linker_file_list target_directories)
    foreach(target ${ARGN})
        if(TARGET ${target})
            get_target_property(target_libraries ${target} INTERFACE_LINK_LIBRARIES)
            get_target_property(target_link_libraries ${target} LINK_LIBRARIES)
            get_target_property(target_type ${target} TYPE)

            if((CMAKE_CXX_COMPILER_ID STREQUAL "MSVC") AND (@FORCE_RELEASE_RUNTIME@))
                if(NOT target_type STREQUAL "INTERFACE_LIBRARY")
                    set_property(TARGET ${target} PROPERTY MSVC_RUNTIME_LIBRARY "MultiThreadedDLL")
                    get_target_property(isImported ${target} IMPORTED)
                    if(NOT isImported)
                        target_compile_options(${target} PRIVATE "/MD")
                    endif()
                endif()
            endif()

            if(target_libraries)
                #get info for targets libs
                _ue4_get_target_link_libraries(TRUE ${target_file_list} ${target_linker_file_list} ${target_directories} ${target_libraries})
            endif()

            if(${addTarget})
                if(target_type STREQUAL "SHARED_LIBRARY")
                    list(APPEND ${target_file_list} "\"$<TARGET_FILE:${target}>\"")
                    list(APPEND ${target_directories} "\"$<TARGET_FILE_DIR:${target}>\"")
                endif()
                if(target_type STREQUAL "STATIC_LIBRARY"
                    OR target_type STREQUAL "SHARED_LIBRARY")
                    list(APPEND ${target_linker_file_list} "$<TARGET_LINKER_FILE:${target}>")
                endif ()
            endif()
        else()
            #not a target, need to hunt for library to include
            #currently ignored, use targets instead of files
        endif()
    endforeach()

    if(${target_file_list})
        list(REMOVE_DUPLICATES ${target_file_list})
        set(${target_file_list} ${${target_file_list}} PARENT_SCOPE)
    endif()
    if(${target_directories})
        list(REMOVE_DUPLICATES ${target_directories})
        set(${target_directories} ${${target_directories}} PARENT_SCOPE)
    endif()
    if(${target_linker_file_list})
        list(REMOVE_DUPLICATES ${target_linker_file_list})
        set(${target_linker_file_list} ${${target_linker_file_list}} PARENT_SCOPE)
    endif()


endfunction()

macro(ue4_get_target_link_libraries target_file_list target_linker_file_list target_directories)
    _ue4_get_target_link_libraries(TRUE ${target_file_list} ${target_linker_file_list} ${target_directories} ${ARGN})
endmacro()

macro(target_build_info build_info target)
    list(APPEND target_includes $<TARGET_PROPERTY:${target},PUBLIC_INCLUDE_DIRECTORIES>)
    list(APPEND target_includes $<TARGET_PROPERTY:${target},INTERFACE_INCLUDE_DIRECTORIES>)
    list(REMOVE_DUPLICATES target_includes)

    list(APPEND target_file_dependencies "@BUILD_TARGET_DIR@/CMakeLists.txt")
    list(APPEND target_file_dependencies $<REMOVE_DUPLICATES:$<TARGET_PROPERTY:${target},CMAKE_CONFIGURE_DEPENDS>>)

    list(APPEND target_file_source_dependencies $<REMOVE_DUPLICATES:$<TARGET_PROPERTY:${target},SOURCES>>)

    ue4_get_target_link_libraries(target_binary_libs target_link_lib_files target_binary_dirs ${target})

    string(REPLACE ";" "$<SEMICOLON>" target_includes "${target_includes}")
    string(REPLACE ";" "$<SEMICOLON>" target_file_dependencies "${target_file_dependencies}")
    string(REPLACE ";" "$<SEMICOLON>" target_file_source_dependencies "${target_file_source_dependencies}")
    string(REPLACE ";" "$<SEMICOLON>" target_binary_libs "${target_binary_libs}")
    string(REPLACE ";" "$<SEMICOLON>" target_binary_dirs "${target_binary_dirs}")
    string(REPLACE ";" "$<SEMICOLON>" target_link_lib_files "${target_link_lib_files}")

    list(APPEND ${build_info} "cppStandard=$<TARGET_PROPERTY:${target},CXX_STANDARD>")
    list(APPEND ${build_info} "includes=$<JOIN:$<REMOVE_DUPLICATES:${target_includes}>,$<COMMA>>")
    list(APPEND ${build_info} "dependencies=$<JOIN:$<REMOVE_DUPLICATES:${target_file_dependencies}>,$<COMMA>>")
    list(APPEND ${build_info} "sourcePath=$<TARGET_PROPERTY:${target},SOURCE_DIR>")
    list(APPEND ${build_info} "sourceDependencies=$<JOIN:$<REMOVE_DUPLICATES:${target_file_source_dependencies}>,$<COMMA>>")
    list(APPEND ${build_info} "binaries=$<JOIN:$<REMOVE_DUPLICATES:${target_binary_libs}>,$<COMMA>>")
    list(APPEND ${build_info} "binaryDirectories=$<JOIN:${target_binary_dirs},$<COMMA>>")
    list(APPEND ${build_info} "libraries=$<JOIN:$<REMOVE_DUPLICATES:${target_link_lib_files}>,$<COMMA>>")
endmacro()
