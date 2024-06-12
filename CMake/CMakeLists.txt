
cmake_minimum_required(VERSION 3.22)

project(Example VERSION 0.1 LANGUAGES CXX)

add_library(Example SHARED Example.cpp)

include(cmake/ExportToUnrealEngine.cmake)
target_build_info("build_info" Example)
string(REPLACE ";" "\n" build_info "${build_info}")
file(GENERATE OUTPUT "${CMAKE_BINARY_DIR}/buildinfo_$<CONFIG>.output" CONTENT "${build_info}")
